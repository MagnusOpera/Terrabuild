module Build
open System
open System.Collections.Generic
open Collections
open Notification

type BuildOptions = {
    MaxConcurrency: int
    NoCache: bool
    Retry: bool
}

let private isTaskUnsatisfied = function
    | TaskBuildStatus.Failure depId -> Some depId
    | TaskBuildStatus.Unfulfilled depId -> Some depId
    | TaskBuildStatus.Success _ -> None

let run (workspaceConfig: Configuration.WorkspaceConfig) (buildBatches: Optimizer.BuildBatches) (cache: Cache.Cache) (notification: IBuildNotification) (options: BuildOptions) =
    let variables = workspaceConfig.Build.Variables

    let isBuildSuccess = function
        | TaskBuildStatus.Success _ -> true
        | _ -> false

    // collect dependencies status
    let getDependencyStatus depId =
        let depNode = buildBatches.Graph.Nodes[depId]
        let depCacheEntryId = $"{depNode.ProjectId}/{depNode.Configuration.Hash}/{depNode.TargetId}"
        match cache.TryGetSummary depCacheEntryId with
        | Some summary -> 
            match summary.Status with
            | Cache.TaskStatus.Success -> TaskBuildStatus.Success depCacheEntryId
            | Cache.TaskStatus.Failure -> TaskBuildStatus.Failure depCacheEntryId
        | _ -> TaskBuildStatus.Unfulfilled depCacheEntryId

    let buildNode (node: Graph.Node) =
        notification.BuildNodeStarted node

        let projectDirectory =
            match IO.combinePath workspaceConfig.Directory node.ProjectId with
            | IO.Directory projectDirectory -> projectDirectory
            | IO.File projectFile -> IO.parentDirectory projectFile
            | _ -> failwith $"Failed to find project '{node.ProjectId}"

        let steps = node.Configuration.Steps[node.TargetId]
        let nodeHash = node.Configuration.Hash
        let cacheEntryId = $"{node.ProjectId}/{nodeHash}/{node.TargetId}"
        let nodeTargetHash = cacheEntryId |> String.sha256

        let unsatisfyingDep =
            node.Dependencies
            |> Seq.map getDependencyStatus
            |> Seq.choose isTaskUnsatisfied |> Seq.tryHead

        match unsatisfyingDep with
        | None ->
            let variables =
                variables
                |> Map.replace node.Configuration.Variables
                |> Map.add "terrabuild_node_hash" nodeHash
                |> Map.add "terrabuild_target_hash" nodeTargetHash

            // check first if it's possible to restore previously built state
            let summary =
                if options.NoCache then None
                else
                    // take care of retrying failed tasks
                    match cache.TryGetSummary cacheEntryId with
                    | Some summary ->
                        if summary.Status = Cache.TaskStatus.Failure then None
                        else Some summary
                    | _ -> None

            let cleanOutputs () =
                node.Configuration.Outputs
                |> Seq.map (IO.combinePath projectDirectory)
                |> Seq.iter IO.deleteAny

            match summary with
            | Some summary ->
                // cleanup before restoring outputs
                cleanOutputs()

                match summary.Outputs with
                | Some outputs ->
                    let files = IO.enumerateFiles outputs
                    IO.copyFiles projectDirectory outputs files |> ignore
                | _ -> ()

                let buildStatus =
                    if summary.Status = Cache.TaskStatus.Success then TaskBuildStatus.Success cacheEntryId
                    else TaskBuildStatus.Failure cacheEntryId
                notification.BuildNodeCompleted node buildStatus

            | _ ->
                let cacheEntry = cache.CreateEntry cacheEntryId

                if node.IsLeaf then
                    cleanOutputs()

                let beforeFiles = FileSystem.createSnapshot projectDirectory node.Configuration.Ignores

                let stepLogs = List<Cache.StepSummary>()
                let mutable lastExitCode = 0
                let mutable stepIndex = 0
                let usedVariables = HashSet<string>()
                while stepIndex < steps.Length && lastExitCode = 0 do
                    let startedAt = DateTime.UtcNow
                    let step = steps[stepIndex]
                    let logFile = cacheEntry.NextLogFile()
                    stepIndex <- stepIndex + 1                        

                    let setVariables s =
                        variables
                        |> Map.fold (fun step key value ->
                            let newStep = step |> String.replace $"$({key})" value
                            if newStep <> step then usedVariables.Add(key) |> ignore
                            newStep) s

                    let step = { step with Arguments = step.Arguments |> setVariables }

                    let exitCode = Exec.execCaptureTimestampedOutput projectDirectory step.Command step.Arguments logFile
                    let endedAt = DateTime.UtcNow
                    let duration = endedAt - startedAt
                    let stepLog = { Cache.StepSummary.Command = step.Command
                                    Cache.StepSummary.Arguments = step.Arguments
                                    Cache.StepSummary.StartedAt = startedAt
                                    Cache.StepSummary.EndedAt = endedAt
                                    Cache.StepSummary.Duration = duration
                                    Cache.StepSummary.Log = logFile
                                    Cache.StepSummary.ExitCode = exitCode }
                    stepLog |> stepLogs.Add
                    lastExitCode <- exitCode

                let afterFiles = FileSystem.createSnapshot projectDirectory node.Configuration.Ignores

                // keep only new or modified files
                let newFiles = afterFiles - beforeFiles 

                // create an archive with new files
                let entryOutputsDir = cacheEntry.Outputs
                let outputs = IO.copyFiles entryOutputsDir projectDirectory newFiles

                let touchedVariables = usedVariables |> Seq.map (fun key -> key, variables[key]) |> Map.ofSeq

                let status =
                    if lastExitCode = 0 then Cache.TaskStatus.Success
                    else Cache.TaskStatus.Failure

                let summary = { Cache.TargetSummary.Project = node.ProjectId
                                Cache.TargetSummary.Target = node.TargetId
                                Cache.TargetSummary.Files = node.Configuration.Files
                                Cache.TargetSummary.Ignores = node.Configuration.Ignores
                                Cache.TargetSummary.Variables = touchedVariables
                                Cache.TargetSummary.Steps = stepLogs |> List.ofSeq
                                Cache.TargetSummary.Outputs = outputs
                                Cache.TargetSummary.Status = status }
                cacheEntry.Complete summary

                let buildStatus =
                    if summary.Status = Cache.TaskStatus.Success then TaskBuildStatus.Success cacheEntryId
                    else TaskBuildStatus.Failure cacheEntryId
                notification.BuildNodeCompleted node buildStatus
        | Some unsatisfyingDep ->
            notification.BuildNodeCompleted node (TaskBuildStatus.Unfulfilled unsatisfyingDep)

    notification.BuildStarted ()
    let startedAt = DateTime.UtcNow

    buildBatches.Batches
    |> Seq.iteri (fun batchNum batch ->
        Threading.ParExec (fun node -> async { buildNode node }) batch.Nodes options.MaxConcurrency |> ignore)

    let headCommit = Git.getHeadCommit workspaceConfig.Directory

    let dependencies =
        buildBatches.Graph.RootNodes
        |> Seq.map (fun (KeyValue(dependency, nodeId)) -> dependency, getDependencyStatus nodeId)
        |> Map.ofSeq

    let endedAt = DateTime.UtcNow
    let duration = endedAt - startedAt

    let status =
        let isSuccess = dependencies |> Seq.forall (fun (KeyValue(_, value)) -> isBuildSuccess value)
        if isSuccess then BuildStatus.Success
        else BuildStatus.Failure

    let buildInfo = { BuildSummary.Commit = headCommit
                      BuildSummary.StartedAt = startedAt
                      BuildSummary.EndedAt = endedAt
                      BuildSummary.Duration = duration
                      BuildSummary.Status = status
                      BuildSummary.Target = buildBatches.Graph.Target
                      BuildSummary.Dependencies = dependencies }
    notification.BuildCompleted buildInfo
