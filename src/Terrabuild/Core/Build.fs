module Build
open System
open System.Collections.Generic
open Collections
open Serilog
open Terrabuild.PubSub
open Errors

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
    Nodes: string set
    RequiredNodes: string set
    BuildNodes: string set
    BuildNodesStatus: NodeStatus set
}


type IBuildNotification =
    abstract WaitCompletion: unit -> unit

    abstract BuildStarted: graph:GraphDef.Graph -> unit
    abstract BuildCompleted: summary:Summary -> unit

    abstract NodeScheduled: node:GraphDef.Node -> unit
    abstract NodeDownloading: node:GraphDef.Node -> unit
    abstract NodeBuilding: node:GraphDef.Node -> unit
    abstract NodeUploading: node:GraphDef.Node -> unit
    abstract NodeCompleted: node:GraphDef.Node -> restored: bool -> success: bool -> unit


let private containerInfos = Concurrent.ConcurrentDictionary<string, string>()

let execCommands (node: GraphDef.Node) (cacheEntry: Cache.IEntry) (options: Configuration.Options) projectDirectory homeDir =
    // run actions if any
    let allCommands =
        node.Operations
        |> List.map (fun operation ->
            let cmd = "docker"
            let wsDir = Environment.CurrentDirectory

            let getContainerUser (container: string) =
                match containerInfos.TryGetValue(container) with
                | true, whoami ->
                    Log.Debug("Reusing USER {whoami} for {container}", whoami, container)
                    whoami
                | _ ->
                    // discover USER
                    let args = $"run --rm --name {node.TargetHash} --entrypoint whoami {container}"
                    let whoami =
                        Log.Debug("Identifying USER for {container}", container)
                        match Exec.execCaptureOutput options.Workspace cmd args with
                        | Exec.Success (whoami, 0) -> whoami.Trim()
                        | _ ->
                            Log.Debug("USER identification failed for {container}: using root", container)
                            "root"

                    Log.Debug("Using USER {whoami} for {container}", whoami, container)
                    containerInfos.TryAdd(container, whoami) |> ignore
                    whoami

            let metaCommand = operation.MetaCommand

            match operation.Container, options.NoContainer with
            | Some container, false ->
                let whoami = getContainerUser container
                let envs =
                    operation.ContainerVariables
                    |> Seq.map (fun var -> $"-e {var}")
                    |> String.join " "
                let args = $"run --rm --net=host --name {node.TargetHash} -v /var/run/docker.sock:/var/run/docker.sock -v {homeDir}:/{whoami} -v {wsDir}:/terrabuild -w /terrabuild/{projectDirectory} --entrypoint {operation.Command} {envs} {container} {operation.Arguments}"
                metaCommand, options.Workspace, cmd, args, operation.Container
            | _ -> metaCommand, projectDirectory, operation.Command, operation.Arguments, operation.Container)

    let stepLogs = List<Cache.OperationSummary>()
    let mutable lastExitCode = 0
    let mutable cmdLineIndex = 0
    let cmdFirstStartedAt = DateTime.UtcNow
    let mutable cmdLastEndedAt = cmdFirstStartedAt

    while cmdLineIndex < allCommands.Length && lastExitCode = 0 do
        let startedAt =
            if cmdLineIndex > 0 then DateTime.UtcNow
            else cmdFirstStartedAt
        let metaCommand, workDir, cmd, args, container = allCommands[cmdLineIndex]
        cmdLineIndex <- cmdLineIndex + 1

        Log.Debug("{Hash}: Running '{Command}' with '{Arguments}'", node.TargetHash, cmd, args)
        let logFile = cacheEntry.NextLogFile()
        let exitCode = Exec.execCaptureTimestampedOutput workDir cmd args logFile
        cmdLastEndedAt <- DateTime.UtcNow
        let endedAt = cmdLastEndedAt
        let duration = endedAt - startedAt
        let stepLog = { Cache.OperationSummary.MetaCommand = metaCommand
                        Cache.OperationSummary.Command = cmd
                        Cache.OperationSummary.Arguments = args
                        Cache.OperationSummary.Container = container
                        Cache.OperationSummary.StartedAt = startedAt
                        Cache.OperationSummary.EndedAt = endedAt
                        Cache.OperationSummary.Duration = duration
                        Cache.OperationSummary.Log = logFile
                        Cache.OperationSummary.ExitCode = exitCode }
        stepLog |> stepLogs.Add
        lastExitCode <- exitCode
        Log.Debug("{Hash}: Execution completed with '{Code}'", node.TargetHash, exitCode)

    lastExitCode, stepLogs

let run (options: Configuration.Options) (sourceControl: Contracts.ISourceControl) (cache: Cache.ICache) (api: Contracts.IApiClient option) (notification: IBuildNotification) (graph: GraphDef.Graph) =
    let targets = options.Targets |> String.join " "
    $"{Ansi.Emojis.rocket} Running targets [{targets}]" |> Terminal.writeLine

    let nodesToBuild = graph.Nodes |> Seq.filter (fun (KeyValue(_,node)) -> node.TargetOperation.IsSome) |> Seq.length
    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} {nodesToBuild} tasks to build" |> Terminal.writeLine

    let startedAt = DateTime.UtcNow
    notification.BuildStarted graph
    let buildId =
        api |> Option.map (fun api -> api.BuildStart sourceControl.BranchOrTag sourceControl.HeadCommit options.Configuration options.Note options.Tag options.Targets options.Force options.Retry sourceControl.CI.IsSome sourceControl.CI sourceControl.Metadata)
        |> Option.defaultValue ""

    let allowRemoteCache = options.LocalOnly |> not

    let homeDir = cache.CreateHomeDir "container"

    let processNode (node: GraphDef.Node) =
        let cacheEntryId = GraphDef.buildCacheKey node

        let projectDirectory =
            match node.Project with
            | FS.Directory projectDirectory -> projectDirectory
            | FS.File projectFile -> FS.parentDirectory projectFile
            | _ -> "."

        let buildNode () =
            let startedAt = DateTime.UtcNow

            notification.NodeBuilding node
            let cacheEntry = cache.GetEntry sourceControl.CI.IsSome node.IsFirst cacheEntryId

            let beforeFiles =
                if node.IsLeaf then IO.Snapshot.Empty // FileSystem.createSnapshot projectDirectory node.Outputs
                else IO.createSnapshot node.Outputs projectDirectory

            let lastExitCode, stepLogs = execCommands node cacheEntry options projectDirectory homeDir

            let successful = lastExitCode = 0
            if successful then Log.Debug("{Hash}: Marking as success", node.TargetHash)
            else Log.Debug("{Hash}: Marking as failed", node.TargetHash)

            let afterFiles = IO.createSnapshot node.Outputs projectDirectory

            // keep only new or modified files
            let newFiles = afterFiles - beforeFiles
            let outputs = IO.copyFiles cacheEntry.Outputs projectDirectory newFiles

            let endedAt = DateTime.UtcNow
            let summary = { Cache.TargetSummary.Project = node.Project
                            Cache.TargetSummary.Target = node.Target
                            Cache.TargetSummary.Operations = [ stepLogs |> List.ofSeq ]
                            Cache.TargetSummary.Outputs = outputs
                            Cache.TargetSummary.IsSuccessful = successful
                            Cache.TargetSummary.StartedAt = startedAt
                            Cache.TargetSummary.EndedAt = endedAt
                            Cache.TargetSummary.Duration = endedAt - startedAt }

            if node.IsLast then
                notification.NodeUploading node

                // create an archive with new files
                Log.Debug("{Hash}: Building '{Project}/{Target}'", node.TargetHash, node.Project, node.Target)
                let cacheEntry = cache.GetEntry sourceControl.CI.IsSome false cacheEntryId
                let files, size = cacheEntry.Complete summary
                api |> Option.iter (fun api -> api.BuildAddArtifact buildId node.Project node.Target node.ProjectHash node.TargetHash files size successful)
            else
                cacheEntry.CompleteLogFile summary

            if successful |> not then
                TerrabuildException.Raise($"Node {node.Id} failed with exit code {lastExitCode}")

        let restoreNode () =
            notification.NodeDownloading node
            let cacheEntryId = GraphDef.buildCacheKey node
            match cache.TryGetSummary allowRemoteCache cacheEntryId with
            | Some summary ->
                Log.Debug("{Hash}: Restoring '{Project}/{Target}' from cache", node.TargetHash, node.Project, node.Target)
                match summary.Outputs with
                | Some outputs ->
                    let files = IO.enumerateFiles outputs
                    IO.copyFiles projectDirectory outputs files |> ignore
                    api |> Option.iter (fun api -> api.BuildUseArtifact buildId node.ProjectHash node.TargetHash)
                | _ -> ()
            | _ ->
                TerrabuildException.Raise($"Unable to download build output for {cacheEntryId} for node {node.Id}")

        try
            if node.TargetOperation.IsSome then buildNode()
            else restoreNode()
            if node.IsLast then notification.NodeCompleted node node.TargetOperation.IsNone true
            else notification.NodeScheduled node
        with
            | exn ->
                Log.Fatal(exn, "Build node failed")
                notification.NodeCompleted node node.TargetOperation.IsNone false
                reraise()


    let scheduledNodes = Concurrent.ConcurrentDictionary<string, bool>()
    let hub = Hub.Create(options.MaxConcurrency)
    let rec schedule nodeId =
        if scheduledNodes.TryAdd(nodeId, true) then
            let node = graph.Nodes[nodeId]
            let nodeComputed = hub.CreateComputed<GraphDef.Node> nodeId

            // await dependencies
            let awaitedDependencies =
                node.Dependencies
                |> Seq.map (fun awaitedProjectId ->
                    schedule awaitedProjectId
                    hub.GetComputed<GraphDef.Node> awaitedProjectId)
                |> Array.ofSeq

            let awaitedSignals = awaitedDependencies |> Array.map (fun entry -> entry :> ISignal)
            hub.Subscribe awaitedSignals (fun () ->
                processNode node
                nodeComputed.Value <- node)

    graph.RootNodes |> Seq.iter schedule

    let status = hub.WaitCompletion()
    match status with
    | Status.Ok -> Log.Debug("Build successful")
    | Status.SubcriptionNotRaised projectId -> Log.Debug("Build failed: project {projectId} is unknown", projectId)
    | Status.SubscriptionError exn -> Log.Debug(exn, "Build failed with exception")

    let headCommit = sourceControl.HeadCommit
    let branchOrTag = sourceControl.BranchOrTag
    let buildNodes = graph.Nodes |> Map.filter (fun _ node -> node.TargetOperation.IsSome)

    // status of nodes to build
    let buildNodesStatus =
        // collect dependencies status
        let getDependencyStatus _ (node: GraphDef.Node) =
            let cacheEntryId = GraphDef.buildCacheKey node
            let nodeInfo = 
                { NodeInfo.Project = node.Project
                  NodeInfo.Target = node.Target
                  NodeInfo.ProjectHash = node.ProjectHash 
                  NodeInfo.NodeHash = node.TargetHash }

            match cache.TryGetSummaryOnly false cacheEntryId with
            | Some (_, summary) ->
                if summary.IsSuccessful then NodeStatus.Success nodeInfo
                else NodeStatus.Failure nodeInfo
            | _ -> NodeStatus.Unfulfilled nodeInfo

        graph.RootNodes
        |> Seq.map (fun depId -> depId, graph.Nodes[depId])
        |> Map.ofSeq
        |> Map.map getDependencyStatus
        |> Map.values
        |> Set.ofSeq

    let status =
        let isBuildSuccess = function
            | NodeStatus.Success _ -> true
            | _ -> false

        let isSuccess = buildNodesStatus |> Seq.forall isBuildSuccess
        if isSuccess then Status.Success
        else Status.Failure

    let endedAt = DateTime.UtcNow
    let buildDuration = endedAt - startedAt
    let totalDuration = endedAt - options.StartedAt

    let buildInfo = { Summary.Commit = headCommit
                      Summary.BranchOrTag = branchOrTag
                      Summary.StartedAt = options.StartedAt
                      Summary.EndedAt = endedAt
                      Summary.BuildDuration = buildDuration
                      Summary.TotalDuration = totalDuration
                      Summary.Status = status
                      Summary.Targets = options.Targets
                      Summary.Nodes = graph.Nodes |> Map.keys |> Set.ofSeq
                      Summary.RequiredNodes = scheduledNodes.Keys |> Set.ofSeq
                      Summary.BuildNodes = buildNodes |> Map.keys |> Set.ofSeq
                      Summary.BuildNodesStatus = buildNodesStatus  }

    notification.BuildCompleted buildInfo
    api |> Option.iter (fun api -> api.BuildComplete buildId (status = Status.Success))

    buildInfo
