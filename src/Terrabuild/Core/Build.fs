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
    ProjectHash: string
    NodeHash: string
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
    ImpactedNodes: string set
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



let run (configuration: Configuration.Workspace) (graph: Graph.Workspace) (cache: Cache.ICache) (api: Contracts.IApiClient option) (notification: IBuildNotification) (options: Configuration.Options) =
    let targets = graph.Targets |> String.join ","
    let targetLabel = if graph.Targets.Count > 1 then "targets" else "target"
    $"{Ansi.Emojis.rocket} Running {targetLabel} {targets}" |> Terminal.writeLine

    let nodesToRun = graph.Nodes |> Seq.filter (fun (KeyValue(_, node)) -> node.Required) |> Seq.length
    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} {nodesToRun} tasks to run" |> Terminal.writeLine

    let startedAt = DateTime.UtcNow
    notification.BuildStarted graph
    let buildId =
        api |> Option.map (fun api -> api.BuildStart configuration.SourceControl.BranchOrTag configuration.SourceControl.HeadCommit configuration.Configuration configuration.Note configuration.Tag graph.Targets options.Force options.Retry configuration.SourceControl.CI)
        |> Option.defaultValue ""

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
        let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.Hash}"
        let nodeInfo = 
            { NodeInfo.Project = node.Project
              NodeInfo.Target = node.Target
              NodeInfo.NodeHash = node.Hash
              NodeInfo.ProjectHash = node.ProjectHash }

        // NOTE: always hit local cache here
        match cache.TryGetSummaryOnly false cacheEntryId with
        | Some summary -> 
            match summary.Status with
            | Cache.TaskStatus.Success -> NodeStatus.Success nodeInfo
            | Cache.TaskStatus.Failure -> NodeStatus.Failure nodeInfo
        | _ -> NodeStatus.Unfulfilled nodeInfo

    let buildNode (node: Graph.Node) =
        // determine if step node can be reused or not
        let useRemoteCache = Cacheability.Never <> (node.Cache &&& cacheMode)
        let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.Hash}"

        notification.NodeDownloading node
        let isAllSatisfied =
            node.Dependencies
            |> Seq.map getDependencyStatus
            |> Seq.choose isNodeUnsatisfied
            |> Seq.isEmpty

        if isAllSatisfied then
            if node.Required then
                let projectDirectory =
                    match node.Project with
                    | FS.Directory projectDirectory -> projectDirectory
                    | FS.File projectFile -> FS.parentDirectory projectFile
                    | _ -> "."

                // check first if it's possible to restore previously built state
                let summary =
                    if options.Force || node.Cache = Cacheability.Never then None
                    else
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
                            batch.Actions |> List.mapi (fun index commandLine ->
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

                                let metaCommand =
                                    if index = 0 then batch.MetaCommand
                                    else "+++"

                                match batch.Container with
                                | None -> metaCommand, projectDirectory, commandLine.Command, commandLine.Arguments, batch.Container
                                | Some container ->
                                    let whoami = getContainerUser container
                                    let envs =
                                        batch.ContainerVariables
                                        |> Seq.map (fun var -> $"-e {var}")
                                        |> String.join " "
                                    let args = $"run --rm --net=host --name {node.Hash} -v /var/run/docker.sock:/var/run/docker.sock -v {homeDir}:/{whoami} -v {wsDir}:/terrabuild -w /terrabuild/{projectDirectory} --entrypoint {commandLine.Command} {envs} {container} {commandLine.Arguments}"
                                    metaCommand, workspaceDir, cmd, args, batch.Container))

                    let beforeFiles =
                        if node.IsLeaf then IO.Snapshot.Empty // FileSystem.createSnapshot projectDirectory node.Outputs
                        else IO.createSnapshot node.Outputs projectDirectory

                    let stepLogs = List<Cache.StepSummary>()
                    let mutable lastExitCode = 0
                    let mutable cmdLineIndex = 0
                    let cmdFirstStartedAt = DateTime.UtcNow
                    let mutable cmdLastEndedAt = cmdFirstStartedAt

                    while cmdLineIndex < allCommands.Length && lastExitCode = 0 do
                        let startedAt =
                            if cmdLineIndex > 0 then DateTime.UtcNow
                            else cmdFirstStartedAt
                        let metaCommand, workDir, cmd, args, container = allCommands[cmdLineIndex]
                        let logFile = cacheEntry.NextLogFile()                    
                        cmdLineIndex <- cmdLineIndex + 1

                        Log.Debug("{Hash}: Running '{Command}' with '{Arguments}'", node.Hash, cmd, args)
                        let exitCode = Exec.execCaptureTimestampedOutput workDir cmd args logFile
                        cmdLastEndedAt <- DateTime.UtcNow
                        let endedAt = cmdLastEndedAt
                        let duration = endedAt - startedAt
                        let stepLog = { Cache.StepSummary.MetaCommand = metaCommand
                                        Cache.StepSummary.Command = cmd
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
                    let afterFiles = IO.createSnapshot node.Outputs projectDirectory

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
                                    Cache.TargetSummary.Status = status
                                    Cache.TargetSummary.StartedAt = cmdFirstStartedAt
                                    Cache.TargetSummary.EndedAt = cmdLastEndedAt }
                    let files, size = cacheEntry.Complete summary
                    api |> Option.iter (fun api -> api.BuildAddArtifact buildId node.Project node.Target node.ProjectHash node.Hash files size (status = Cache.TaskStatus.Success))
                    Some summary, false
            else
                cache.TryGetSummaryOnly useRemoteCache cacheEntryId, false
        else
            None, false

    // this is the core of the build
    // schedule first nodes with no incoming edges
    // on completion schedule released nodes
    let restoredNodes = Concurrent.ConcurrentDictionary<string, bool>()
    let buildQueue = Exec.BuildQueue(options.MaxConcurrency)
    let rec queueAction (nodeId: string) =
        let node = graph.Nodes[nodeId]

        let summary, restored = buildNode node
        notification.NodeCompleted node restored summary
        restoredNodes.TryAdd(nodeId, restored) |> ignore

        // schedule children nodes if ready
        let triggers = reverseIncomings[nodeId]
        for trigger in triggers do
            let newValue = System.Threading.Interlocked.Decrement(readyNodes[trigger])
            if newValue = 0 then
                readyNodes[trigger].Value <- -1 // mark node as scheduled
                buildQueue.Enqueue (fun () -> queueAction trigger)


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

    let impactedNodes =
        restoredNodes
        |> Seq.choose (fun (KeyValue(nodeId, restored)) -> if restored then None else Some nodeId)
        |> Set

    let buildInfo = { Summary.Commit = headCommit
                      Summary.BranchOrTag = branchOrTag
                      Summary.StartedAt = options.StartedAt
                      Summary.EndedAt = endedAt
                      Summary.BuildDuration = buildDuration
                      Summary.TotalDuration = totalDuration
                      Summary.Status = status
                      Summary.Targets = graph.Targets
                      Summary.ImpactedNodes = impactedNodes
                      Summary.RootNodes = dependencies }

    notification.BuildCompleted buildInfo
    api |> Option.iter (fun api -> api.BuildComplete buildId (status = Status.Success))

    buildInfo
