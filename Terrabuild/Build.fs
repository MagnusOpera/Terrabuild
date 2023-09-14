module Build
open Graph
open Helpers
open System.Collections.Generic


type BuildInfo = {
    Commit: string
    Dependencies: string list
}

let extractCommandFromArgs (commandline: string) =
    let idx = commandline.IndexOf(' ')
    if idx = -1 then failwith $"Invalid command '{commandline}"
    let command = commandline.Substring(0, idx)
    let args = commandline.Substring(idx)
    command, args

let run (workspaceDirectory: string) (g: WorkspaceGraph) =

    let rec buildDependencies (nodeIds: string seq) =
        nodeIds |> Seq.map buildDependency |> List.ofSeq

    and buildDependency nodeId =
        let node = g.Nodes[nodeId]

        // compute node hash:
        // - hash of dependencies
        // - listing
        let dependenciesHashes = buildDependencies node.Dependencies
        let nodeHash = node.Listing :: dependenciesHashes |> String.join "\n" |> String.sha256

        // check first if it's possible to restore previously built state
        let summary = BuildCache.getBuildSummary nodeHash
        let summary =
            match summary with
            | Some summary ->
                printfn $"Reusing build cache for {node.TargetId}@{node.ProjectId}"
                summary
            | _ -> 
                printfn $"Building {node.TargetId}@{node.ProjectId}:"
                let target = node.Configuration.Targets[node.TargetId]
                let stepLogs = List<string>()
                let mutable lastExitCode = 0
                let mutable stepIndex = 0
                while stepIndex < target.Steps.Length && lastExitCode = 0 do
                    let step = target.Steps[stepIndex]
                    stepIndex <- stepIndex + 1                        

                    let projectDirectory = IO.combine workspaceDirectory node.ProjectId
                    let command, args = extractCommandFromArgs step
                    let execResult = Exec.execCaptureTimestampedOutput projectDirectory command args
                    match execResult with
                    | Exec.Success (logFile, exitCode) ->
                        stepLogs.Add(logFile)
                        lastExitCode <- exitCode
                    | Exec.Error (logfile, exitCode) -> 
                        stepLogs.Add(logfile)
                        lastExitCode <- exitCode

                let summary = { BuildCache.ProjectId = node.ProjectId
                                BuildCache.TargetId = node.TargetId
                                BuildCache.Listing = node.Listing
                                BuildCache.Dependencies = dependenciesHashes
                                BuildCache.StepLogs = stepLogs |> List.ofSeq
                                BuildCache.ExitCode = lastExitCode }
                BuildCache.writeBuildSummary nodeHash summary
                summary

        if summary.ExitCode = 0 then nodeHash
        else failwith "Build failure"

    let headCommit = Git.getHeadCommit workspaceDirectory
    let dependencies = buildDependencies g.RootNodes

    let buildInfo = { Commit = headCommit
                      Dependencies = dependencies }
    printfn $"Build completed"
    buildInfo
