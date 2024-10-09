module Build
open System
open System.Collections.Generic
open Collections
open Serilog
open Terrabuild.PubSub
open Errors

[<RequireQualifiedAccess>]
type TaskRequest =
    | Restore
    | Build

[<RequireQualifiedAccess>]
type TaskStatus =
    | Success of completionDate:DateTime
    | Failure of completionDate:DateTime * message:string

[<RequireQualifiedAccess>]
type NodeInfo = {
    Request: TaskRequest
    Status: TaskStatus
    Project: string
    Target: string
    ProjectHash: string
    TargetHash: string
}

[<RequireQualifiedAccess>]
type Summary = {
    Commit: string
    BranchOrTag: string
    StartedAt: DateTime
    EndedAt: DateTime
    TotalDuration: TimeSpan
    BuildDuration: TimeSpan
    IsSuccess: bool
    Targets: string set
    Nodes: Map<string, NodeInfo>
}



type IBuildNotification =
    abstract WaitCompletion: unit -> unit

    abstract BuildStarted: graph:GraphDef.Graph -> unit
    abstract BuildCompleted: summary:Summary -> unit

    abstract NodeScheduled: node:GraphDef.Node -> unit
    abstract NodeDownloading: node:GraphDef.Node -> unit
    abstract NodeBuilding: node:GraphDef.Node -> unit
    abstract NodeUploading: node:GraphDef.Node -> unit
    abstract NodeCompleted: node:GraphDef.Node -> request:TaskRequest -> success:bool -> unit


let private containerInfos = Concurrent.ConcurrentDictionary<string, string>()

let execCommands (node: GraphDef.Node) (cacheEntry: Cache.IEntry) (options: Configuration.Options) projectDirectory homeDir tmpDir =
    // run actions if any
    let allCommands =
        node.Operations
        |> List.map (fun operation ->
            let cmd = "docker"
            let wsDir = Environment.CurrentDirectory

            let getContainerUserHome (container: string) =
                match containerInfos.TryGetValue(container) with
                | true, containerHome ->
                    Log.Debug("Reusing USER {containerHome} for {container}", containerHome, container)
                    containerHome
                | _ ->
                    // discover USER
                    let args = $"run --rm --name {node.TargetHash} --entrypoint sh {container} \"echo -n \\$HOME\""
                    let containerHome =
                        Log.Debug("Identifying USER for {container}", container)
                        match Exec.execCaptureOutput options.Workspace cmd args with
                        | Exec.Success (containerHome, 0) -> containerHome.Trim()
                        | _ ->
                            Log.Debug("USER identification failed for {container}: using root", container)
                            "/root"

                    Log.Debug("Using USER {containerHome} for {container}", containerHome, container)
                    containerInfos.TryAdd(container, containerHome) |> ignore
                    containerHome

            let metaCommand = operation.MetaCommand

            match operation.Container, options.NoContainer with
            | Some container, false ->
                let containerHome = getContainerUserHome container
                let envs =
                    operation.ContainerVariables
                    |> Seq.map (fun var -> $"-e {var}")
                    |> String.join " "
                let args = $"run --rm --net=host --name {node.TargetHash} --pid=host --ipc=host -v /var/run/docker.sock:/var/run/docker.sock -v {homeDir}:{containerHome} -v {tmpDir}:/tmp -v {wsDir}:/terrabuild -w /terrabuild/{projectDirectory} --entrypoint {operation.Command} {envs} {container} {operation.Arguments}"
                metaCommand, options.Workspace, cmd, args, operation.Container, operation.ExitCodes
            | _ -> metaCommand, projectDirectory, operation.Command, operation.Arguments, operation.Container, operation.ExitCodes)

    let stepLogs = List<Cache.OperationSummary>()
    let mutable lastStatusCode = Terrabuild.Extensibility.StatusCode.Ok false
    let mutable cmdLineIndex = 0
    let cmdFirstStartedAt = DateTime.UtcNow
    let mutable cmdLastEndedAt = cmdFirstStartedAt

    while cmdLineIndex < allCommands.Length && lastStatusCode.IsOkish do
        let startedAt =
            if cmdLineIndex > 0 then DateTime.UtcNow
            else cmdFirstStartedAt
        let metaCommand, workDir, cmd, args, container, exitCodes = allCommands[cmdLineIndex]
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

        let statusCode =
            match exitCodes |> Map.tryFind exitCode with
            | Some statusCode -> statusCode
            | _ -> Terrabuild.Extensibility.StatusCode.Error exitCode
        lastStatusCode <- statusCode
        Log.Debug("{Hash}: Execution completed with exit code '{Code}' ({Status})", node.TargetHash, exitCode, lastStatusCode)

    lastStatusCode, stepLogs

let run (options: Configuration.Options) (sourceControl: Contracts.ISourceControl) (cache: Cache.ICache) (api: Contracts.IApiClient option) (notification: IBuildNotification) (graph: GraphDef.Graph) =
    let targets = options.Targets |> String.join " "
    $"{Ansi.Emojis.rocket} Running targets [{targets}]" |> Terminal.writeLine

    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} {graph.Nodes.Count} tasks to build" |> Terminal.writeLine

    let startedAt = DateTime.UtcNow
    notification.BuildStarted graph
    let buildId =
        api |> Option.map (fun api -> api.StartBuild sourceControl.BranchOrTag sourceControl.HeadCommit options.Configuration options.Note options.Tag options.Targets options.Force options.Retry sourceControl.CI.IsSome sourceControl.CI sourceControl.Metadata)
        |> Option.defaultValue ""

    let allowRemoteCache = options.LocalOnly |> not

    let homeDir = cache.CreateHomeDir "containers"
    let tmpDir = cache.CreateHomeDir "tmp"

    let tryGetSummaryOnly id =
        let allowRemoteCache = options.LocalOnly |> not
        cache.TryGetSummaryOnly allowRemoteCache id |> Option.map (fun (_, summary) -> summary)

    let force = options.Force
    let retry = options.Retry

    let nodeResults = Concurrent.ConcurrentDictionary<string, TaskRequest * TaskStatus>()

    let processNode (maxCompletionChildren: DateTime) (node: GraphDef.Node) =
        let cacheEntryId = GraphDef.buildCacheKey node

        let projectDirectory =
            match node.Project with
            | FS.Directory projectDirectory -> projectDirectory
            | FS.File projectFile -> FS.parentDirectory projectFile
            | _ -> "."

        let buildNode currentCompletionDate =
            let startedAt = DateTime.UtcNow
            let isBuild = currentCompletionDate = DateTime.MaxValue

            notification.NodeBuilding node

            let beforeFiles =
                if node.IsLeaf then IO.Snapshot.Empty
                else IO.createSnapshot node.Outputs projectDirectory

            let cacheEntry = cache.GetEntry (isBuild && sourceControl.CI.IsSome) cacheEntryId
            let lastStatusCode, stepLogs = execCommands node cacheEntry options projectDirectory homeDir tmpDir

            // keep only new or modified files
            let afterFiles = IO.createSnapshot node.Outputs projectDirectory
            let newFiles = afterFiles - beforeFiles
            let outputs = IO.copyFiles cacheEntry.Outputs projectDirectory newFiles

            let successful = lastStatusCode.IsOkish
            let endedAt = DateTime.UtcNow
            let summary = { Cache.TargetSummary.Project = node.Project
                            Cache.TargetSummary.Target = node.Target
                            Cache.TargetSummary.Operations = [ stepLogs |> List.ofSeq ]
                            Cache.TargetSummary.Outputs = outputs
                            Cache.TargetSummary.IsSuccessful = successful
                            Cache.TargetSummary.StartedAt = startedAt
                            Cache.TargetSummary.EndedAt = endedAt
                            Cache.TargetSummary.Duration = endedAt - startedAt
                            Cache.TargetSummary.Cache = node.Cache }

            notification.NodeUploading node

            // create an archive with new files
            Log.Debug("{NodeId}: Building '{Project}/{Target}' with {Hash}", node.Id, node.Project, node.Target, node.TargetHash)
            let files = cacheEntry.Complete summary
            api |> Option.iter (fun api -> api.AddArtifact buildId node.Project node.Target node.ProjectHash node.TargetHash files successful)

            match lastStatusCode with
            | Terrabuild.Extensibility.StatusCode.Ok true ->
                TaskStatus.Success endedAt
            | Terrabuild.Extensibility.StatusCode.Ok false ->
                TaskStatus.Success currentCompletionDate
            | Terrabuild.Extensibility.StatusCode.Error _ ->
                TaskStatus.Failure (DateTime.UtcNow, $"{node.Id} failed with exit code {lastStatusCode}")

        let restoreNode () =
            notification.NodeDownloading node

            let cacheEntryId = GraphDef.buildCacheKey node
            match cache.TryGetSummary allowRemoteCache cacheEntryId with
            | Some summary ->
                Log.Debug("{NodeId}: Restoring '{Project}/{Target}' from cache from {Hash}", node.Id, node.Project, node.Target, node.TargetHash)
                match summary.Outputs with
                | Some outputs ->
                    let files = IO.enumerateFiles outputs
                    IO.copyFiles projectDirectory outputs files |> ignore
                    api |> Option.iter (fun api -> api.UseArtifact buildId node.ProjectHash node.TargetHash)
                | _ -> ()
                TaskStatus.Success summary.EndedAt
            | _ ->
                TaskStatus.Failure (DateTime.UtcNow, $"Unable to download build output for {cacheEntryId} for node {node.Id}")

        let buildRequest, completionDate =
            if force then
                Log.Debug("{nodeId} must rebuild because force build requested", node.Id)
                TaskRequest.Build, buildNode DateTime.MaxValue
            elif maxCompletionChildren = DateTime.MaxValue then
                Log.Debug("{nodeId} must rebuild because child is rebuilding", node.Id)
                TaskRequest.Build, buildNode DateTime.MaxValue
            elif node.Cache <> Terrabuild.Extensibility.Cacheability.Never then
                let cacheEntryId = GraphDef.buildCacheKey node
                match tryGetSummaryOnly cacheEntryId with
                | Some summary ->
                    Log.Debug("{nodeId} has existing build summary", node.Id)
                    // task is younger than children
                    if summary.StartedAt < maxCompletionChildren then
                        Log.Debug("{nodeId} must rebuild because it is younger than child", node.Id)
                        TaskRequest.Build, buildNode DateTime.MaxValue
                    // task is failed and retry requested
                    elif retry && not summary.IsSuccessful then
                        Log.Debug("{nodeId} must rebuild because node is failed and retry requested", node.Id)
                        TaskRequest.Build, buildNode DateTime.MaxValue
                    // task is dynamic
                    elif (node.Cache &&& Terrabuild.Extensibility.Cacheability.Dynamic) <> Terrabuild.Extensibility.Cacheability.Never then
                        Log.Debug("{nodeId} is dynamic, checking if state has changed", node.Id)
                        let completionStatus = buildNode summary.EndedAt
                        match completionStatus with
                        | TaskStatus.Failure _ -> TaskRequest.Build, completionStatus
                        | TaskStatus.Success completionDate when summary.EndedAt = completionDate ->
                            Log.Debug("{nodeId} state has not changed", node.Id)
                            TaskRequest.Restore, completionStatus
                        | _ ->
                            Log.Debug("{nodeId} state has changed, keeping changes", node.Id)
                            TaskRequest.Build, completionStatus
                    // task is cached
                    else
                        Log.Debug("{nodeId} is marked as used", node.Id)
                        TaskRequest.Restore, restoreNode()
                | _ ->
                    Log.Debug("{nodeId} must be build since no summary and required", node.Id)
                    TaskRequest.Build, buildNode DateTime.MaxValue
            else
                Log.Debug("{nodeId} is not cacheable", node.Id)
                TaskRequest.Build, buildNode DateTime.MaxValue

        buildRequest, completionDate


    let scheduledNodes = Concurrent.ConcurrentDictionary<string, bool>()
    let hub = Hub.Create(options.MaxConcurrency)
    let rec schedule nodeId =
        if scheduledNodes.TryAdd(nodeId, true) then
            let node = graph.Nodes[nodeId]
            let nodeComputed = hub.CreateComputed<DateTime> nodeId

            // await dependencies
            let awaitedDependencies =
                node.Dependencies
                |> Seq.map (fun awaitedProjectId ->
                    schedule awaitedProjectId
                    hub.GetComputed<DateTime> awaitedProjectId)
                |> Array.ofSeq

            let awaitedSignals = awaitedDependencies |> Array.map (fun entry -> entry :> ISignal)
            hub.Subscribe awaitedSignals (fun () ->
                try
                    let maxCompletionChildren =
                        awaitedDependencies
                        |> Seq.map (fun dep -> dep.Value)
                        |> Seq.sortDescending
                        |> Seq.tryHead
                        |> Option.defaultValue DateTime.MinValue

                    let buildRequest, completionStatus = processNode maxCompletionChildren node
                    Log.Debug("{nodeId} has completed for request {Request} with status {Status}", node.Id, buildRequest, completionStatus)
                    nodeResults.TryAdd(node.Id, (buildRequest, completionStatus)) |> ignore

                    match completionStatus with
                    | TaskStatus.Success completionDate ->
                        nodeComputed.Value <- completionDate
                        notification.NodeCompleted node buildRequest true
                    | _ ->
                        notification.NodeCompleted node buildRequest false
                with
                    exn ->
                        Log.Fatal(exn, $"{nodeId} unexpected failure while building")

                        nodeResults.TryAdd(node.Id, (TaskRequest.Build, TaskStatus.Failure (DateTime.UtcNow, exn.Message))) |> ignore
                        notification.NodeCompleted node TaskRequest.Build false

                        reraise())

    graph.RootNodes |> Seq.iter schedule

    let status = hub.WaitCompletion()
    match status with
    | Status.Ok -> Log.Debug("Build successful")
    | Status.SubcriptionNotRaised projectId -> Log.Debug("Build failed: project {projectId} is not processed", projectId)
    | Status.SubscriptionError exn -> Log.Fatal(exn, "Build failed with exception")

    let headCommit = sourceControl.HeadCommit
    let branchOrTag = sourceControl.BranchOrTag

    let endedAt = DateTime.UtcNow
    let buildDuration = endedAt - startedAt
    let totalDuration = endedAt - options.StartedAt

    let nodeStatus =
        let getDependencyStatus _ (node: GraphDef.Node) =
            match nodeResults.TryGetValue node.Id with
            | true, (request, status) ->
                { NodeInfo.Request = request
                  NodeInfo.Status = status
                  NodeInfo.Project = node.Project
                  NodeInfo.Target = node.Target
                  NodeInfo.ProjectHash = node.ProjectHash 
                  NodeInfo.TargetHash = node.TargetHash } |> Some
            | _ -> None

        graph.Nodes
        |> Map.choose getDependencyStatus

    let isSuccess =
        graph.Nodes.Count = nodeStatus.Count
        && nodeStatus |> Map.forall (fun _ nodeInfo -> match nodeInfo.Status with | TaskStatus.Success _ -> true | _ -> false)

    let buildInfo = { Summary.Commit = headCommit
                      Summary.BranchOrTag = branchOrTag
                      Summary.StartedAt = options.StartedAt
                      Summary.EndedAt = endedAt
                      Summary.BuildDuration = buildDuration
                      Summary.TotalDuration = totalDuration
                      Summary.IsSuccess = isSuccess
                      Summary.Targets = options.Targets
                      Summary.Nodes = nodeStatus }

    notification.BuildCompleted buildInfo
    api |> Option.iter (fun api -> api.CompleteBuild buildId isSuccess)

    buildInfo
