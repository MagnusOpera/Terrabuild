module Build
open System
open System.Collections.Generic


type BuildInfo = {
    Commit: string
    Target: string
    Dependencies: Map<string, string>
}


let getExecInfo info =
    if info |> Map.containsKey "^shell" then
        info["^shell"], info["args"]
    elif info |> Map.containsKey "^dotnet" then
        let dotnetCommand = info["^dotnet"]
        let dotnetConfig = info |> Map.tryFind "configuration" |> Option.defaultValue "Debug"
        let args = info |> Map.tryFind "args" |> Option.defaultValue ""
        "dotnet", $"{dotnetCommand} --no-dependencies --configuration {dotnetConfig} {args}"
    else
        failwith "Unknown step"

let run (workspaceConfig: Configuration.WorkspaceConfig) (g: Graph.WorkspaceGraph) =

    let variableContext = workspaceConfig.Build.Variables

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
        // - list of files (without outputs)
        // - files hash
        // - variables dependencies

        let variables =
            let extractVariables s =
                match s with
                | String.Regex "\$\(([a-z]+)\)" variables -> variables
                | _ -> []

            steps
            |> Seq.collect (fun kvp -> kvp.Values |> Seq.collect extractVariables )
            |> Seq.map (fun var -> var, variableContext[var])
            |> Map.ofSeq

        let variableHashes =
            variables
            |> Seq.map (fun kvp -> $"{kvp.Key} = {kvp.Value}")
            |> String.join "\n"

        let dependenciesHashes = buildDependencies node.Dependencies
        let nodeHash =
            dependenciesHashes @ node.Files @ [ node.FilesHash ; variableHashes ]
            |> String.join "\n" |> String.sha256

        // check first if it's possible to restore previously built state
        let summary = BuildCache.getBuildSummary nodeHash
        let summary =
            match summary with
            | Some summary ->
                printfn $"Reusing build cache for {node.TargetId}@{node.ProjectId}: {nodeHash}"

                // cleanup before restoring outputs
                node.Configuration.Outputs
                |> Seq.map (IO.combine projectDirectory)
                |> Seq.iter IO.deleteAny

                Zip.restoreArchive summary.Outputs projectDirectory
                summary
            | _ -> 
                printfn $"Building {node.TargetId}@{node.ProjectId}: {nodeHash}"

                let beforeOutputs = FileSystem.createSnapshot projectDirectory node.Configuration.Outputs

                let stepLogs = List<BuildCache.StepInfo>()
                let mutable lastExitCode = 0
                let mutable stepIndex = 0
                while stepIndex < steps.Length && lastExitCode = 0 do
                    let stepInfo = steps[stepIndex]
                    stepIndex <- stepIndex + 1                        

                    let setVariables s =
                        variables
                        |> Map.fold (fun step key value -> step |> String.replace $"$({key})" value) s

                    let stepInfo = stepInfo |> Map.map (fun _ v -> setVariables v)

                    let command, args = getExecInfo stepInfo

                    let beginExecution = System.Diagnostics.Stopwatch.StartNew()
                    let exitCode, logFile = Exec.execCaptureTimestampedOutput projectDirectory command args
                    let executionDuration = beginExecution.Elapsed
                    let stepLog = { BuildCache.Command = command
                                    BuildCache.Args = args
                                    BuildCache.Duration = executionDuration
                                    BuildCache.Log = logFile }
                    stepLog |> stepLogs.Add
                    lastExitCode <- exitCode

                let afterOutputs = FileSystem.createSnapshot projectDirectory node.Configuration.Outputs

                // keep only new or modified files
                let newOutputs = afterOutputs - beforeOutputs 

                // create an archive with new files
                let outputArchive = Zip.createArchive projectDirectory newOutputs

                let summary = { BuildCache.Project = node.ProjectId
                                BuildCache.Target = node.TargetId
                                BuildCache.Files = node.Files
                                BuildCache.FilesHash = node.FilesHash
                                BuildCache.Variables = variables
                                BuildCache.Dependencies = dependenciesHashes
                                BuildCache.Steps = stepLogs |> List.ofSeq
                                BuildCache.Outputs = outputArchive
                                BuildCache.ExitCode = lastExitCode }
                BuildCache.writeBuildSummary nodeHash summary

        if summary.ExitCode = 0 then nodeHash
        else
            let content = summary.Steps |> List.last |> (fun x -> x.Log) |> IO.readTextFile 
            printfn $"Build failure for node hash {nodeHash}:\n{content}"
            failwith $"Build failure for node hash {nodeHash}"

    let headCommit = Git.getHeadCommit workspaceConfig.Directory
    let dependencies = g.RootNodes |> Map.map (fun k v -> buildDependency v)
    let buildInfo = { Commit = headCommit
                      Target = g.Target
                      Dependencies = dependencies }
    printfn $"Build completed"
    buildInfo
