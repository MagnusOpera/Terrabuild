module Build
open System
open System.Collections.Generic

type BuildInfo = {
    Commit: string
    Target: string
    Dependencies: Map<string, string>
}

let run (workspaceConfig: Configuration.WorkspaceConfig) (g: Graph.WorkspaceGraph) =
    let variables = workspaceConfig.Build.Variables

    let rec buildDependencies (nodeIds: string seq) =
        nodeIds
        |> Seq.map buildDependency
        |> List.ofSeq

    and buildDependency nodeId =
        let node = g.Nodes[nodeId]
        let projectDirectory = IO.combine workspaceConfig.Directory node.ProjectId
        let steps = node.Configuration.Steps[node.TargetId]

        // compute node hash:
        // - hash of dependencies
        // - list of files (without outputs & ignores)
        // - files hash
        // - variables dependencies

        let dependenciesHashes = buildDependencies node.Dependencies

        let nodeHash = node.Configuration.Hash

        let nodeTargetHash = $"{node.TargetId}/{nodeHash}"
        let cacheEntryId = IO.combine node.ProjectId nodeTargetHash

        let variables =
            variables
            |> Map.add "terrabuild_node_hash" nodeHash

        // check first if it's possible to restore previously built state
        let summary = BuildCache.getBuildSummary cacheEntryId

        let cleanOutputs () =
            node.Configuration.Outputs
            |> Seq.map (IO.combine projectDirectory)
            |> Seq.iter IO.deleteAny

        let summary =
            match summary with
            | Some summary ->
                printfn $"Reusing build cache for {node.TargetId}@{node.ProjectId}: {cacheEntryId}"

                // cleanup before restoring outputs
                cleanOutputs()

                Zip.restoreArchive summary.Outputs projectDirectory
                summary
            | _ -> 
                printfn $"Building {node.TargetId}@{node.ProjectId}: {cacheEntryId}"
                let startedAt = DateTime.UtcNow

                if node.IsLeaf then cleanOutputs()

                let beforeFiles = FileSystem.createSnapshot projectDirectory node.Configuration.Ignores

                let stepLogs = List<BuildCache.StepInfo>()
                let mutable lastExitCode = 0
                let mutable stepIndex = 0
                while stepIndex < steps.Length && lastExitCode = 0 do
                    let step = steps[stepIndex]
                    stepIndex <- stepIndex + 1                        

                    let setVariables s =
                        variables
                        |> Map.fold (fun step key value -> step |> String.replace $"$({key})" value) s

                    let step = { step
                                 with Arguments = step.Arguments |> setVariables }

                    let beginExecution = System.Diagnostics.Stopwatch.StartNew()
                    let exitCode, logFile = Exec.execCaptureTimestampedOutput projectDirectory step.Command step.Arguments
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
                let outputArchive = Zip.createArchive projectDirectory newFiles

                let summary = { BuildCache.Project = node.ProjectId
                                BuildCache.Target = node.TargetId
                                BuildCache.Files = node.Configuration.Files
                                BuildCache.Ignores = node.Configuration.Ignores
                                BuildCache.Variables = variables
                                BuildCache.Steps = stepLogs |> List.ofSeq
                                BuildCache.Outputs = outputArchive
                                BuildCache.ExitCode = lastExitCode }
                BuildCache.writeBuildSummary cacheEntryId summary

        if summary.ExitCode = 0 then nodeTargetHash
        else
            let content = summary.Steps |> List.last |> (fun x -> x.Log) |> IO.readTextFile 
            printfn $"Build failure for node hash {cacheEntryId}:\n{content}"
            failwith $"Build failure for node hash {cacheEntryId}"

    let headCommit = Git.getHeadCommit workspaceConfig.Directory
    let dependencies = g.RootNodes |> Map.map (fun k v -> buildDependency v)
    let buildInfo = { Commit = headCommit
                      Target = g.Target
                      Dependencies = dependencies }
    printfn $"Build completed"
    buildInfo
