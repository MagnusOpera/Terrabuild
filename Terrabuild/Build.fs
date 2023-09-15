module Build
open Graph
open Helpers
open System
open System.Collections.Generic
open Configuration


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

let run (workspaceConfig: WorkspaceConfig) (g: WorkspaceGraph) =

    let variableContext =
        workspaceConfig.Build.Variables |> Option.defaultValue Map.empty

    let rec buildDependencies (nodeIds: string seq) =
        nodeIds
        |> Seq.map buildDependency
        |> List.ofSeq

    and buildDependency nodeId =
        let node = g.Nodes[nodeId]
        let projectDirectory = IO.combine workspaceConfig.Directory node.ProjectId
        let target = node.Configuration.Targets[node.TargetId]

        // compute node hash:
        // - hash of dependencies
        // - tree files (with hash)
        // - local changes
        // - variables dependencies

        let variables =
            let extractVariables s =
                match s with
                | String.Regex "\$\(([a-z]+)\)" variables -> variables
                | _ -> []

            target.Steps
            |> List.collect extractVariables
            |> Seq.map (fun var -> var, variableContext[var])
            |> Map.ofSeq

        let variableHashes =
            variables
            |> Seq.map (fun kvp -> $"{kvp.Key} = {kvp.Value}")
            |> String.join "\n"

        let dependenciesHashes = buildDependencies node.Dependencies
        let nodeHash =
            dependenciesHashes @ [ node.TreeFiles ; node.Changes; variableHashes ]
            |> String.join "\n" |> String.sha256

        // check first if it's possible to restore previously built state
        let summary = BuildCache.getBuildSummary nodeHash
        let summary =
            match summary with
            | Some summary ->
                printfn $"Reusing build cache for {node.TargetId}@{node.ProjectId}"

                // cleanup before restoring outputs
                node.Configuration.Outputs
                |> Seq.map (IO.combine projectDirectory)
                |> Seq.iter IO.deleteAny

                Zip.restoreArchive summary.Outputs projectDirectory
                summary
            | _ -> 
                printfn $"Building {node.TargetId}@{node.ProjectId}:"

                let enumerateFileInfos (outputs: string seq) =
                    outputs
                    |> Seq.map (IO.combine projectDirectory)
                    |> Seq.collect (fun output ->
                        match output with
                        | IO.File _ -> [ output, System.IO.File.GetLastWriteTimeUtc output ]
                        | IO.Directory _ -> System.IO.Directory.EnumerateFiles(output, "*", System.IO.SearchOption.AllDirectories)
                                            |> Seq.map (fun file -> file, System.IO.File.GetLastWriteTimeUtc file)
                                            |> List.ofSeq
                        | _ -> [])
                    |> Map.ofSeq

                let beforeOutputs = enumerateFileInfos node.Configuration.Outputs

                let stepLogs = List<BuildCache.StepInfo>()
                let mutable lastExitCode = 0
                let mutable stepIndex = 0
                while stepIndex < target.Steps.Length && lastExitCode = 0 do
                    let step = target.Steps[stepIndex]
                    stepIndex <- stepIndex + 1                        

                    let step =
                        variables
                        |> Map.fold (fun step key value -> step |> String.replace $"$({key})" value) step

                    let command, args = extractCommandFromArgs step
                    let beginExecution = System.Diagnostics.Stopwatch.StartNew()
                    let exitCode, logFile = Exec.execCaptureTimestampedOutput projectDirectory command args
                    let executionDuration = beginExecution.Elapsed
                    let stepLog = { BuildCache.Command = step
                                    BuildCache.Duration = executionDuration
                                    BuildCache.Log = logFile }
                    stepLog |> stepLogs.Add
                    lastExitCode <- exitCode

                let afterOutputs = enumerateFileInfos node.Configuration.Outputs

                // remove files that have not changed
                let newOutputs =
                    afterOutputs
                    |> Seq.choose (fun afterOutput ->
                        match beforeOutputs |> Map.tryFind afterOutput.Key with
                        | Some prevWriteDate when afterOutput.Value = prevWriteDate -> None
                        | _ -> Some afterOutput.Key)
                    |> List.ofSeq

                // create an archive with new files
                let outputArchive = Zip.createArchive projectDirectory newOutputs

                let summary = { BuildCache.Project = node.ProjectId
                                BuildCache.Target = node.TargetId
                                BuildCache.TreeFiles = node.TreeFiles
                                BuildCache.Changes = node.Changes
                                BuildCache.Variables = variables
                                BuildCache.Dependencies = dependenciesHashes
                                BuildCache.Steps = stepLogs |> List.ofSeq
                                BuildCache.Outputs = outputArchive
                                BuildCache.ExitCode = lastExitCode }
                BuildCache.writeBuildSummary nodeHash summary
                summary

        if summary.ExitCode = 0 then nodeHash
        else failwith "Build failure"

    let headCommit = Git.getHeadCommit workspaceConfig.Directory
    let dependencies = buildDependencies g.RootNodes

    let buildInfo = { Commit = headCommit
                      Dependencies = dependencies }
    printfn $"Build completed"
    buildInfo
