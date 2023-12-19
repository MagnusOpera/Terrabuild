module Build
open System
open System.Collections.Generic
open Collections

[<RequireQualifiedAccess>]
type NodeInfo = {
    Project: string
    Target: string
    Hash: string
}

[<RequireQualifiedAccess>]
type NodeBuildStatus =
    | Success of NodeInfo
    | Failure of NodeInfo
    | Unfulfilled of NodeInfo

[<RequireQualifiedAccess>]
type BuildStatus =
    | Success
    | Failure

[<RequireQualifiedAccess>]
type BuildSummary = {
    Commit: string
    BranchOrTag: string
    StartedAt: DateTime
    EndedAt: DateTime
    Duration: TimeSpan
    Status: BuildStatus
    Targets: string set
    RootNodes: NodeBuildStatus set
}

type IBuildNotification =
    abstract WaitCompletion: unit -> unit

    abstract BuildStarted: graph:Graph.WorkspaceGraph -> unit
    abstract BuildCompleted: summary:BuildSummary -> unit

    abstract NodeScheduled: node:Graph.Node -> unit
    abstract NodeDownloading: node:Graph.Node -> unit
    abstract NodeBuilding: node:Graph.Node -> unit
    abstract NodeUploading: node:Graph.Node -> unit
    abstract NodeCompleted: node:Graph.Node -> summary:Cache.TargetSummary option -> unit



let private isNodeUnsatisfied = function
    | NodeBuildStatus.Failure nodeInfo -> Some nodeInfo
    | NodeBuildStatus.Unfulfilled nodeInfo -> Some nodeInfo
    | NodeBuildStatus.Success _ -> None


let run (workspaceConfig: Configuration.WorkspaceConfig) (graph: Graph.WorkspaceGraph) (cache: Cache.ICache) (notification: IBuildNotification) (options: Configuration.Options) =

    let cacheMode =
        if options.CI then Extensions.Cacheability.Always
        else Extensions.Cacheability.Remote

    // compute first incoming edges
    let reverseIncomings =
        graph.Nodes
        |> Map.map (fun _ _ -> List<string>())
    for KeyValue(nodeId, node) in graph.Nodes do
        for dependency in node.Dependencies do
            reverseIncomings[dependency].Add(nodeId)

    let allNodes =
        graph.Nodes
        |> Map.map (fun _ _ -> ref 0)

    let refCounts =
        reverseIncomings
        |> Seq.collect (fun kvp -> kvp.Value)
        |> Seq.countBy (id)
        |> Map
        |> Map.map (fun _ value -> ref value)

    let readyNodes =
        allNodes |> Map.replace refCounts

    let isBuildSuccess = function
        | NodeBuildStatus.Success _ -> true
        | _ -> false

    // collect dependencies status
    let getDependencyStatus depId =
        let node = graph.Nodes[depId]
        let cacheEntryId = $"{node.Project}/{node.Target}/{node.Hash}"
        let nodeInfo = 
            { NodeInfo.Project = node.Project
              NodeInfo.Target = node.Target
              NodeInfo.Hash = node.Hash }

        // NOTE: always hit local cache here
        match cache.TryGetSummary false cacheEntryId with
        | Some summary -> 
            match summary.Status with
            | Cache.TaskStatus.Success -> NodeBuildStatus.Success nodeInfo
            | Cache.TaskStatus.Failure -> NodeBuildStatus.Failure nodeInfo
        | _ -> NodeBuildStatus.Unfulfilled nodeInfo

    let buildNode (node: Graph.Node) =
        notification.NodeDownloading node
        let isAllSatisfied =
            node.Dependencies
            |> Seq.map getDependencyStatus
            |> Seq.choose isNodeUnsatisfied
            |> Seq.isEmpty

        if isAllSatisfied then
            let projectDirectory =
                match IO.combinePath workspaceConfig.Directory node.Project with
                | IO.Directory projectDirectory -> projectDirectory
                | IO.File projectFile -> IO.parentDirectory projectFile
                | _ -> failwith $"Failed to find project {node.Project}"

            let cacheEntryId = $"{node.Project}/{node.Target}/{node.Hash}"

            // check first if it's possible to restore previously built state
            let summary =
                if options.NoCache || node.Cache = Extensions.Cacheability.Never then None
                else
                    // determine if step node can be reused or not
                    let useRemoteCache = Extensions.Cacheability.Never <> (node.Cache &&& cacheMode)

                    // get task execution summary & take care of retrying failed tasks
                    match cache.TryGetSummary useRemoteCache cacheEntryId with
                    | Some summary when summary.Status = Cache.TaskStatus.Failure && options.Retry -> None
                    | Some summary -> Some summary
                    | _ -> None

            // clean outputs if leaf node (otherwise outputs are layered on top of previous ones)
            if node.IsLeaf then
                node.Outputs
                |> Seq.map (IO.combinePath projectDirectory)
                |> Seq.iter IO.deleteAny

            match summary with
            | Some summary ->
                match summary.Outputs with
                | Some outputs ->
                    let files = IO.enumerateFiles outputs
                    IO.copyFiles projectDirectory outputs files |> ignore
                | _ -> ()
                Some summary

            | _ ->
                let cacheEntry = cache.CreateEntry options.CI cacheEntryId
                notification.NodeBuilding node

                let beforeFiles = FileSystem.createSnapshot projectDirectory node.Outputs

                let stepLogs = List<Cache.StepSummary>()
                let mutable lastExitCode = 0
                let mutable cmdLineIndex = 0
                while cmdLineIndex < node.CommandLines.Length && lastExitCode = 0 do
                    let startedAt = DateTime.UtcNow
                    let commandLine = node.CommandLines[cmdLineIndex]
                    let logFile = cacheEntry.NextLogFile()
                    let homeDir = cache.CreateHomeDir node.ProjectHash
                    cmdLineIndex <- cmdLineIndex + 1

                    let workDir, cmd, args =
                        match commandLine.Container with
                        | None
                        | Some null ->
                            projectDirectory, commandLine.Command, commandLine.Arguments
                        | Some container ->
                            let cmd = "docker"
                            let wsDir = IO.combinePath Environment.CurrentDirectory workspaceConfig.Directory

                            // NOTE:
                            //  - run command into a dedicated container (entrypoint)
                            //  - whole workspace is mapped in the container and current directory is set to project directory (volume + workdir)
                            //  - redirect home directory as well because we want to mutualize side effects (if any) for this step
                            let args = $"run --entrypoint {commandLine.Command} --rm -v {homeDir}:/root -v {wsDir}:/terrabuild -w /terrabuild/{node.Project} {container} {commandLine.Arguments}"
                            workspaceConfig.Directory, cmd, args

                    let exitCode = Exec.execCaptureTimestampedOutput workDir cmd args logFile
                    let endedAt = DateTime.UtcNow
                    let duration = endedAt - startedAt
                    let stepLog = { Cache.StepSummary.Command = commandLine.Command
                                    Cache.StepSummary.Arguments = commandLine.Arguments
                                    Cache.StepSummary.Container = commandLine.Container
                                    Cache.StepSummary.Variables = node.Variables
                                    Cache.StepSummary.StartedAt = startedAt
                                    Cache.StepSummary.EndedAt = endedAt
                                    Cache.StepSummary.Duration = duration
                                    Cache.StepSummary.Log = logFile
                                    Cache.StepSummary.ExitCode = exitCode }
                    stepLog |> stepLogs.Add
                    lastExitCode <- exitCode

                notification.NodeUploading node
                let afterFiles = FileSystem.createSnapshot projectDirectory node.Outputs

                // keep only new or modified files
                let newFiles = afterFiles - beforeFiles

                // create an archive with new files
                let outputs = IO.copyFiles cacheEntry.Outputs projectDirectory newFiles

                let status =
                    if lastExitCode = 0 then Cache.TaskStatus.Success
                    else Cache.TaskStatus.Failure

                let summary = { Cache.TargetSummary.Project = node.Project
                                Cache.TargetSummary.Target = node.Target
                                Cache.TargetSummary.Steps = stepLogs |> List.ofSeq
                                Cache.TargetSummary.Outputs = outputs
                                Cache.TargetSummary.Status = status }
                cacheEntry.Complete summary
                Some summary
        else
            None

    // this is the core of the build
    // schedule first nodes with no incoming edges
    // on completion schedule released nodes
    let buildQueue = Exec.BuildQueue(options.MaxConcurrency)
    let rec scheduleNode (nodeId: string) =
        let node = graph.Nodes[nodeId]

        let buildAction () = 
            let summary = buildNode node
            notification.NodeCompleted node summary

            // schedule children nodes if ready
            let triggers = reverseIncomings[nodeId]                
            for trigger in triggers do
                let newValue = System.Threading.Interlocked.Decrement(readyNodes[trigger])
                if newValue = 0 then
                    readyNodes[trigger].Value <- -1 // mark node as scheduled
                    scheduleNode trigger

        notification.NodeScheduled node
        buildQueue.Enqueue buildAction


    notification.BuildStarted graph
    let startedAt = DateTime.UtcNow

    readyNodes
    |> Map.filter (fun _ value -> value.Value = 0)
    |> Map.iter (fun key _ -> scheduleNode key)

    // wait only if we have something to do
    buildQueue.WaitCompletion()

    let headCommit = workspaceConfig.SourceControl.HeadCommit
    let branchOrTag = workspaceConfig.SourceControl.BranchOrTag

    let dependencies =
        graph.RootNodes
        |> Seq.map getDependencyStatus
        |> Set

    let endedAt = DateTime.UtcNow
    let duration = endedAt - startedAt

    let status =
        let isSuccess = dependencies |> Seq.forall isBuildSuccess
        if isSuccess then BuildStatus.Success
        else BuildStatus.Failure

    let buildInfo = { BuildSummary.Commit = headCommit
                      BuildSummary.BranchOrTag = branchOrTag
                      BuildSummary.StartedAt = startedAt
                      BuildSummary.EndedAt = endedAt
                      BuildSummary.Duration = duration
                      BuildSummary.Status = status
                      BuildSummary.Targets = graph.Targets
                      BuildSummary.RootNodes = dependencies }
    notification.BuildCompleted buildInfo
    buildInfo