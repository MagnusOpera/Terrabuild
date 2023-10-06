module Build
open System
open System.Collections.Generic

[<RequireQualifiedAccess>]
type BuildStatus =
    | Success of string
    | Failure of string
    | Unsatisfied of string

[<RequireQualifiedAccess>]
type BuildInfo = {
    Commit: string
    Target: string
    Dependencies: Map<string, BuildStatus>
}

let private isBuildUnsatisfied status =
    match status with
    | BuildStatus.Failure depId -> Some depId
    | BuildStatus.Unsatisfied depId -> Some depId
    | BuildStatus.Success _ -> None
 
let run (workspaceConfig: Configuration.WorkspaceConfig) (g: Graph.WorkspaceGraph) (noCache: bool) (cache: BuildCache.Cache) =
    let variables = workspaceConfig.Build.Variables

    let rec buildDependencies (nodeIds: string seq) : BuildStatus list =
        nodeIds
        |> Seq.map buildDependency
        |> List.ofSeq

    and buildDependency nodeId: BuildStatus =
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
        let unsatisfyingDep = dependenciesHashes |> Seq.choose isBuildUnsatisfied |> Seq.tryHead
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

                        let beginExecution = System.Diagnostics.Stopwatch.StartNew()
                        let exitCode = Exec.execCaptureTimestampedOutput projectDirectory step.Command step.Arguments logFile
                        let executionDuration = beginExecution.Elapsed
                        let endedAt = DateTime.UtcNow
                        let stepLog = { BuildCache.Command = step.Command
                                        BuildCache.Arguments = step.Arguments
                                        BuildCache.StartedAt = startedAt
                                        BuildCache.EndedAt = endedAt
                                        BuildCache.Duration = executionDuration
                                        BuildCache.Log = logFile }
                        stepLog |> stepLogs.Add
                        lastExitCode <- exitCode

                    let afterFiles = FileSystem.createSnapshot projectDirectory node.Configuration.Ignores

                    // keep only new or modified files
                    let newFiles = afterFiles - beforeFiles 

                    // create an archive with new files
                    let entryOutputsDir = cacheEntry.Outputs
                    let outputs = IO.copyFiles entryOutputsDir projectDirectory newFiles

                    let touchedVariables = usedVariables |> Seq.map (fun key -> key, variables[key]) |> Map.ofSeq

                    let summary = { BuildCache.Project = node.ProjectId
                                    BuildCache.Target = node.TargetId
                                    BuildCache.Files = node.Configuration.Files
                                    BuildCache.Ignores = node.Configuration.Ignores
                                    BuildCache.Variables = touchedVariables
                                    BuildCache.Steps = stepLogs |> List.ofSeq
                                    BuildCache.Outputs = outputs
                                    BuildCache.ExitCode = lastExitCode }
                    cacheEntry.Complete summary
                    summary

            if summary.ExitCode = 0 then
                cacheEntryId |> BuildStatus.Success
            else
                cacheEntryId |> BuildStatus.Failure
        | Some unsatisfyingDep ->
            unsatisfyingDep |> BuildStatus.Unsatisfied

    let headCommit = Git.getHeadCommit workspaceConfig.Directory
    let dependencies = g.RootNodes |> Map.map (fun k v -> buildDependency v)
    let buildInfo = { BuildInfo.Commit = headCommit
                      BuildInfo.Target = g.Target
                      BuildInfo.Dependencies = dependencies }

    let hasBuildFailure =
        dependencies |> Seq.choose (fun (KeyValue(_, value)) -> isBuildUnsatisfied value) |> Seq.tryHead

    match hasBuildFailure with
    | Some _ -> printfn "Build failed"
    | _ -> printfn "Build succeeded"
    buildInfo
