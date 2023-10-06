module Build
open System
open System.Collections.Generic

[<RequireQualifiedAccess>]
type BuildStatus =
    | Success
    | Failure

[<RequireQualifiedAccess>]
type TaskBuildStatus =
    | Success of string
    | Failure of string
    | Unfulfilled of string

[<RequireQualifiedAccess>]
type BuildInfo = {
    Commit: string
    StartedAt: DateTime
    EndedAt: DateTime
    Duration: TimeSpan
    Status: BuildStatus
    Target: string
    Dependencies: Map<string, TaskBuildStatus>
}

let private isBuildSuccess = function
    | TaskBuildStatus.Success _ -> true
    | _ -> false
 
let private isTaskUnsatisfied = function
    | TaskBuildStatus.Failure depId -> Some depId
    | TaskBuildStatus.Unfulfilled depId -> Some depId
    | TaskBuildStatus.Success _ -> None

let run (workspaceConfig: Configuration.WorkspaceConfig) (g: Graph.WorkspaceGraph) (noCache: bool) (cache: BuildCache.Cache) =
    let variables = workspaceConfig.Build.Variables

    let rec buildDependencies (nodeIds: string seq) : TaskBuildStatus list =
        nodeIds
        |> Seq.map buildDependency
        |> List.ofSeq

    and buildDependency nodeId: TaskBuildStatus =
        let node = g.Nodes[nodeId]
        let projectDirectory =
            match IO.combinePath workspaceConfig.Directory node.ProjectId with
            | IO.Directory projectDirectory -> projectDirectory
            | IO.File projectFile -> IO.parentDirectory projectFile
            | _ -> failwith $"Failed to find project '{node.ProjectId}"
        

        let steps = node.Configuration.Steps[node.TargetId]
        let nodeHash = node.Configuration.Hash
        let cacheEntryId = $"{nodeHash}/{node.TargetId}"
        let nodeTargetHash = cacheEntryId |> String.sha256

        // compute node hash:
        // - hash of dependencies
        // - list of files (without outputs & ignores)
        // - files hash
        // - variables dependencies

        let dependenciesHashes = buildDependencies node.Dependencies
        let unsatisfyingDep = dependenciesHashes |> Seq.choose isTaskUnsatisfied |> Seq.tryHead
        match unsatisfyingDep with
        | None -> 
            let variables =
                variables
                |> Map.add "terrabuild_node_hash" nodeHash
                |> Map.add "terrabuild_target_hash" nodeTargetHash

            // check first if it's possible to restore previously built state
            let summary =
                if noCache then None
                else cache.TryGetSummary cacheEntryId

            let cleanOutputs () =
                node.Configuration.Outputs
                |> Seq.map (IO.combinePath projectDirectory)
                |> Seq.iter IO.deleteAny

            let summary =
                match summary with
                | Some summary ->
                    printfn $"Reusing build cache for {node.TargetId}@{node.ProjectId}: {cacheEntryId}"

                    // cleanup before restoring outputs
                    cleanOutputs()

                    match summary.Outputs with
                    | Some outputs ->
                        let files = IO.enumerateFiles outputs
                        IO.copyFiles projectDirectory outputs files |> ignore
                        summary
                    | _ -> summary

                | _ ->
                    let cacheEntry = cache.CreateEntry cacheEntryId

                    printfn $"Building {node.TargetId}@{node.ProjectId}: {cacheEntryId}"
                    let startedAt = DateTime.UtcNow

                    if node.IsLeaf then
                        printfn $"Cleaning output of leaf task '{cacheEntryId}'"
                        cleanOutputs()

                    let beforeFiles = FileSystem.createSnapshot projectDirectory node.Configuration.Ignores

                    let stepLogs = List<BuildCache.StepInfo>()
                    let mutable lastExitCode = 0
                    let mutable stepIndex = 0
                    let usedVariables = HashSet<string>()
                    while stepIndex < steps.Length && lastExitCode = 0 do
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

                        let startedAt = DateTime.UtcNow
                        let exitCode = Exec.execCaptureTimestampedOutput projectDirectory step.Command step.Arguments logFile
                        let endedAt = DateTime.UtcNow
                        let duration = endedAt - startedAt
                        let stepLog = { BuildCache.StepInfo.Command = step.Command
                                        BuildCache.StepInfo.Arguments = step.Arguments
                                        BuildCache.StepInfo.StartedAt = startedAt
                                        BuildCache.StepInfo.EndedAt = endedAt
                                        BuildCache.StepInfo.Duration = duration
                                        BuildCache.StepInfo.Log = logFile
                                        BuildCache.StepInfo.ExitCode = exitCode }
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
                        if lastExitCode = 0 then BuildCache.TaskStatus.Success
                        else BuildCache.TaskStatus.Failure

                    let summary = { BuildCache.Summary.Project = node.ProjectId
                                    BuildCache.Summary.Target = node.TargetId
                                    BuildCache.Summary.Files = node.Configuration.Files
                                    BuildCache.Summary.Ignores = node.Configuration.Ignores
                                    BuildCache.Summary.Variables = touchedVariables
                                    BuildCache.Summary.Steps = stepLogs |> List.ofSeq
                                    BuildCache.Summary.Outputs = outputs
                                    BuildCache.Summary.Status = status }
                    cacheEntry.Complete summary
                    summary

            match summary.Status with
            | BuildCache.TaskStatus.Success -> TaskBuildStatus.Success cacheEntryId
            | BuildCache.TaskStatus.Failure -> TaskBuildStatus.Failure cacheEntryId

        | Some unsatisfyingDep -> TaskBuildStatus.Unfulfilled unsatisfyingDep

    let startedAt = DateTime.UtcNow
    let headCommit = Git.getHeadCommit workspaceConfig.Directory
    let dependencies = g.RootNodes |> Map.map (fun k v -> buildDependency v)
    let endedAt = DateTime.UtcNow
    let duration = endedAt - startedAt
    let status =
        let isSuccess = dependencies |> Seq.forall (fun (KeyValue(_, value)) -> isBuildSuccess value)
        if isSuccess then BuildStatus.Success
        else BuildStatus.Failure

    let buildInfo = { BuildInfo.Commit = headCommit
                      BuildInfo.StartedAt = startedAt
                      BuildInfo.EndedAt = endedAt
                      BuildInfo.Duration = duration
                      BuildInfo.Status = status
                      BuildInfo.Target = g.Target
                      BuildInfo.Dependencies = dependencies }
    buildInfo
