module Build
open System
open System.Collections.Generic
open Collections
open Serilog
open Terrabuild.Extensibility

[<RequireQualifiedAccess>]
type NodeInfo = {
    Project: string
    Target: string
    Hash: string
}

[<RequireQualifiedAccess>]
type NodeStatus =
    | Success of NodeInfo
    | Failure of NodeInfo
    | Unfulfilled of NodeInfo

[<RequireQualifiedAccess>]
type Status =
    | Success
    | Failure

[<RequireQualifiedAccess>]
type Summary = {
    Commit: string
    BranchOrTag: string
    StartedAt: DateTime
    EndedAt: DateTime
    TotalDuration: TimeSpan
    BuildDuration: TimeSpan
    Status: Status
    Targets: string set
    RootNodes: NodeStatus set
}


type IBuildNotification =
    abstract WaitCompletion: unit -> unit

    abstract BuildStarted: graph:Graph.Workspace -> unit
    abstract BuildCompleted: summary:Summary -> unit

    abstract NodeScheduled: node:Graph.Node -> unit
    abstract NodeDownloading: node:Graph.Node -> unit
    abstract NodeBuilding: node:Graph.Node -> unit
    abstract NodeUploading: node:Graph.Node -> unit
    abstract NodeCompleted: node:Graph.Node -> restored: bool -> summary:Cache.TargetSummary option -> unit


let private isNodeUnsatisfied = function
    | NodeStatus.Failure nodeInfo -> Some nodeInfo
    | NodeStatus.Unfulfilled nodeInfo -> Some nodeInfo
    | NodeStatus.Success _ -> None



let run (configuration: Configuration.Workspace) (graph: Graph.Workspace) (cache: Cache.ICache) (notification: IBuildNotification) (options: Configuration.Options) =
    let startedAt = DateTime.UtcNow

    let workspaceDir = Environment.CurrentDirectory

    let containerInfos = Concurrent.ConcurrentDictionary<string, string>()

    let cacheMode =
        if configuration.SourceControl.CI then Cacheability.Always
        else Cacheability.Remote

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
        allNodes
        |> Map.addMap refCounts

    let isBuildSuccess = function
        | NodeStatus.Success _ -> true
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
            | Cache.TaskStatus.Success -> NodeStatus.Success nodeInfo
            | Cache.TaskStatus.Failure -> NodeStatus.Failure nodeInfo
        | _ -> NodeStatus.Unfulfilled nodeInfo

    let buildNode (node: Graph.Node) =
        notification.NodeDownloading node
        let isAllSatisfied =
            node.Dependencies
            |> Seq.map getDependencyStatus
            |> Seq.choose isNodeUnsatisfied
            |> Seq.isEmpty

        if isAllSatisfied then
            let projectDirectory =
                match node.Project with
                | IO.Directory projectDirectory -> projectDirectory
                | IO.File projectFile -> IO.parentDirectory projectFile
                | _ -> "."

            let cacheEntryId = $"{node.Project}/{node.Target}/{node.Hash}"

            // check first if it's possible to restore previously built state
            let summary =
                if options.Force || node.Cache = Cacheability.Never then None
                else
                    // determine if step node can be reused or not
                    let useRemoteCache = Cacheability.Never <> (node.Cache &&& cacheMode)

                    // get task execution summary & take care of retrying failed tasks
                    match cache.TryGetSummary useRemoteCache cacheEntryId with
                    | Some summary when summary.Status = Cache.TaskStatus.Failure && options.Retry -> None
                    | Some summary -> Some summary
                    | _ -> None

            match summary with
            | Some summary ->
                Log.Debug("{Hash}: Restoring '{Project}/{Target}' from cache", node.Hash, node.Project, node.Target)
                match summary.Outputs with
                | Some outputs ->
                    let files = IO.enumerateFiles outputs
                    IO.copyFiles projectDirectory outputs files |> ignore
                | _ -> ()
                Some summary, true

            | _ ->
                Log.Debug("{Hash}: Building '{Project}/{Target}'", node.Hash, node.Project, node.Target)
                let cacheEntry = cache.CreateEntry configuration.SourceControl.CI cacheEntryId
                notification.NodeBuilding node

                // NOTE:
                //  we use ProjectHash here because it's interesting from a cache perspective
                //  some binaries could have been cached in homedir, let's reuse them if available
                let homeDir = cache.CreateHomeDir node.ProjectHash

                let allCommands =
                    node.CommandLines
                    |> List.collect (fun batch ->
                        batch.Actions |> List.map (fun commandLine ->
                            let cmd = "docker"
                            let wsDir = Environment.CurrentDirectory

                            let getContainerUser (container: string) =
                                match containerInfos.TryGetValue(container) with
                                | true, whoami ->
                                    Log.Debug("Reusing USER {whoami} for {container}", whoami, container)
                                    whoami
                                | _ ->
                                    // discover USER
                                    let args = $"run --rm --name {node.Hash} --entrypoint whoami {container}"
                                    let whoami =
                                        Log.Debug("Identifying USER for {container}", container)
                                        match Exec.execCaptureOutput workspaceDir cmd args with
                                        | Exec.Success (whoami, 0) -> whoami.Trim()
                                        | _ ->
                                            Log.Debug("USER identification failed for {container}: using root", container)
                                            "root"

                                    Log.Debug("Using USER {whoami} for {container}", whoami, container)
                                    containerInfos.TryAdd(container, whoami) |> ignore
                                    whoami

                            match batch.Container with
                            | None -> projectDirectory, commandLine.Command, commandLine.Arguments, batch.Container
                            | Some container ->
                                let whoami = getContainerUser container
                                let args = $"run --rm --entrypoint {commandLine.Command} --name {node.Hash} -v /var/run/docker.sock:/var/run/docker.sock -v {homeDir}:/{whoami} -v {wsDir}:/terrabuild -w /terrabuild/{projectDirectory} {container} {commandLine.Arguments}"
                                workspaceDir, cmd, args, batch.Container))


                let beforeFiles = FileSystem.Snapshot.Empty // FileSystem.createSnapshot projectDirectory node.Outputs

                let stepLogs = List<Cache.StepSummary>()
                let mutable lastExitCode = 0
                let mutable cmdLineIndex = 0

                while cmdLineIndex < allCommands.Length && lastExitCode = 0 do
                    let startedAt = DateTime.UtcNow
                    let workDir, cmd, args, container = allCommands[cmdLineIndex]
                    let logFile = cacheEntry.NextLogFile()                    
                    cmdLineIndex <- cmdLineIndex + 1

                    Log.Debug("{Hash}: Running '{Command}' with '{Arguments}'", node.Hash, cmd, args)
                    let exitCode = Exec.execCaptureTimestampedOutput workDir cmd args logFile
                    let endedAt = DateTime.UtcNow
                    let duration = endedAt - startedAt
                    let stepLog = { Cache.StepSummary.Command = cmd
                                    Cache.StepSummary.Arguments = args
                                    Cache.StepSummary.Container = container
                                    Cache.StepSummary.StartedAt = startedAt
                                    Cache.StepSummary.EndedAt = endedAt
                                    Cache.StepSummary.Duration = duration
                                    Cache.StepSummary.Log = logFile
                                    Cache.StepSummary.ExitCode = exitCode }
                    stepLog |> stepLogs.Add
                    lastExitCode <- exitCode
                    Log.Debug("{Hash}: Execution completed with '{Code}'", node.Hash, exitCode)

                notification.NodeUploading node
                let afterFiles = FileSystem.createSnapshot node.Outputs projectDirectory

                // keep only new or modified files
                let newFiles = afterFiles - beforeFiles

                // create an archive with new files
                let outputs = IO.copyFiles cacheEntry.Outputs projectDirectory newFiles

                let status =
                    if lastExitCode = 0 then
                        Log.Debug("{Hash}: Marking as success", node.Hash)
                        Cache.TaskStatus.Success
                    else
                        Log.Debug("{Hash}: Marking as failed", node.Hash)
                        Cache.TaskStatus.Failure

                let summary = { Cache.TargetSummary.Project = node.Project
                                Cache.TargetSummary.Target = node.Target
                                Cache.TargetSummary.Steps = stepLogs |> List.ofSeq
                                Cache.TargetSummary.Outputs = outputs
                                Cache.TargetSummary.Status = status }
                cacheEntry.Complete summary
                Some summary, false
        else
            None, false

    // this is the core of the build
    // schedule first nodes with no incoming edges
    // on completion schedule released nodes
    let buildQueue = Exec.BuildQueue(options.MaxConcurrency)
    let rec queueAction (nodeId: string) =
        let node = graph.Nodes[nodeId]
        let summary, restored = buildNode node
        notification.NodeCompleted node restored summary

        // schedule children nodes if ready
        let triggers = reverseIncomings[nodeId]
        for trigger in triggers do
            let newValue = System.Threading.Interlocked.Decrement(readyNodes[trigger])
            if newValue = 0 then
                readyNodes[trigger].Value <- -1 // mark node as scheduled
                buildQueue.Enqueue (fun () -> queueAction trigger)

    notification.BuildStarted graph

    readyNodes
    |> Map.filter (fun _ value -> value.Value = 0)
    |> Map.iter (fun key _ -> buildQueue.Enqueue (fun () -> queueAction key))

    // wait only if we have something to do
    buildQueue.WaitCompletion()

    let headCommit = configuration.SourceControl.HeadCommit
    let branchOrTag = configuration.SourceControl.BranchOrTag

    let dependencies =
        graph.RootNodes
        |> Seq.map getDependencyStatus
        |> Set

    let endedAt = DateTime.UtcNow
    let buildDuration = endedAt - startedAt
    let totalDuration = endedAt - options.StartedAt

    let status =
        let isSuccess = dependencies |> Seq.forall isBuildSuccess
        if isSuccess then Status.Success
        else Status.Failure

    let buildInfo = { Summary.Commit = headCommit
                      Summary.BranchOrTag = branchOrTag
                      Summary.StartedAt = options.StartedAt
                      Summary.EndedAt = endedAt
                      Summary.BuildDuration = buildDuration
                      Summary.TotalDuration = totalDuration
                      Summary.Status = status
                      Summary.Targets = graph.Targets
                      Summary.RootNodes = dependencies }
    notification.BuildCompleted buildInfo
    buildInfo
