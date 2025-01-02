module Build
open System
open System.Collections.Generic
open Collections
open Serilog
open Terrabuild.PubSub

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

let isOkish statusCode =
    match statusCode with
    | 0 -> true
    | _ -> false


let execCommands (node: GraphDef.Node) (cacheEntry: Cache.IEntry) (options: ConfigOptions.Options) projectDirectory homeDir tmpDir =
    // run actions if any
    let allCommands =
        node.Operations
        |> List.map (fun operation ->
            let metaCommand = operation.MetaCommand
            match options.ContainerTool, operation.Container with
            | Some cmd, Some container ->
                let wsDir = Environment.CurrentDirectory

                let containerHome =
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

                let envs =
                    operation.ContainerVariables
                    |> Seq.map (fun var -> $"-e {var}")
                    |> String.join " "
                let args = $"run --rm --net=host --name {node.TargetHash} --pid=host --ipc=host -v /var/run/docker.sock:/var/run/docker.sock -v {homeDir}:{containerHome} -v {tmpDir}:/tmp -v {wsDir}:/terrabuild -w /terrabuild/{projectDirectory} --entrypoint {operation.ShellOp.Command} {envs} {container} {operation.ShellOp.Arguments}"
                metaCommand, options.Workspace, cmd, args, operation.Container
            | _ -> metaCommand, projectDirectory, operation.ShellOp.Command, operation.ShellOp.Arguments, operation.Container)
 
    let stepLogs = List<Cache.OperationSummary>()
    let mutable lastStatusCode = 0
    let mutable cmdLineIndex = 0
    let cmdFirstStartedAt = DateTime.UtcNow
    let mutable cmdLastEndedAt = cmdFirstStartedAt

    while cmdLineIndex < allCommands.Length && isOkish lastStatusCode do
        let startedAt =
            if cmdLineIndex > 0 then DateTime.UtcNow
            else cmdFirstStartedAt
        let metaCommand, workDir, cmd, args, container = allCommands[cmdLineIndex]
        cmdLineIndex <- cmdLineIndex + 1

        Log.Debug("{Hash}: Running '{Command}' with '{Arguments}'", node.TargetHash, cmd, args)
        let logFile = cacheEntry.NextLogFile()
        let exitCode =
            if options.Targets |> Set.contains "serve" then
                Exec.execConsole workDir cmd args
            else
                Exec.execCaptureTimestampedOutput workDir cmd args logFile
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

        let statusCode = exitCode
        lastStatusCode <- statusCode
        Log.Debug("{Hash}: Execution completed with exit code '{Code}' ({Status})", node.TargetHash, exitCode, lastStatusCode)

    lastStatusCode, stepLogs





type Restorable(action: unit -> unit, dependencies: Restorable list) =
    let restore = lazy(
        dependencies |> List.iter (fun restorable -> restorable.Restore())
        action()
    )

    member _.Restore() = restore.Force()


let run (options: ConfigOptions.Options) (cache: Cache.ICache) (api: Contracts.IApiClient option) (notification: IBuildNotification) (graph: GraphDef.Graph) =
    let targets = options.Targets |> String.join " "
    $"{Ansi.Emojis.rocket} Running targets [{targets}]" |> Terminal.writeLine

    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} {graph.Nodes.Count} tasks to build" |> Terminal.writeLine

    notification.BuildStarted graph
    api |> Option.iter (fun api -> api.StartBuild())

    let allowRemoteCache = options.CI.IsSome

    let homeDir = cache.CreateHomeDir "containers"
    let tmpDir = cache.CreateHomeDir "tmp"

    let tryGetSummaryOnly id =
        let allowRemoteCache = options.LocalOnly |> not
        cache.TryGetSummaryOnly allowRemoteCache id |> Option.map (fun (_, summary) -> summary)

    let force = options.Force
    let retry = options.Retry
    let checkState = options.CheckState

    let nodeResults = Concurrent.ConcurrentDictionary<string, TaskRequest * TaskStatus>()
    let restorables = Concurrent.ConcurrentDictionary<string, Restorable>()

    let processNode (maxCompletionChildren: DateTime) (node: GraphDef.Node) =
        let cacheEntryId = GraphDef.buildCacheKey node

        let projectDirectory =
            match node.Project with
            | FS.Directory projectDirectory -> projectDirectory
            | FS.File projectFile -> FS.parentDirectory projectFile
            | _ -> "."

        let buildNode currentCompletionDate =
            let startedAt = DateTime.UtcNow
            let allowUpload = retry || currentCompletionDate = DateTime.MaxValue

            notification.NodeBuilding node

            // restore lazy dependencies
            node.Dependencies
            |> Seq.iter (fun nodeId ->
                match restorables.TryGetValue nodeId with
                | true, restorable -> restorable.Restore()
                | _ -> ())

            let beforeFiles =
                if node.IsLeaf then IO.Snapshot.Empty
                else IO.createSnapshot node.Outputs projectDirectory

            let cacheEntry = cache.GetEntry allowUpload cacheEntryId
            let lastStatusCode, stepLogs = execCommands node cacheEntry options projectDirectory homeDir tmpDir

            // keep only new or modified files
            let afterFiles = IO.createSnapshot node.Outputs projectDirectory
            let newFiles = afterFiles - beforeFiles
            let outputs = IO.copyFiles cacheEntry.Outputs projectDirectory newFiles

            let successful = isOkish lastStatusCode
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
            api |> Option.iter (fun api -> api.AddArtifact node.Project node.Target node.ProjectHash node.TargetHash files successful)

            match lastStatusCode with
            | 0 -> TaskStatus.Success endedAt
            | _ -> TaskStatus.Failure (DateTime.UtcNow, $"{node.Id} failed with exit code {lastStatusCode}")


        let restoreNode () =
            notification.NodeScheduled node
            let cacheEntryId = GraphDef.buildCacheKey node
            match tryGetSummaryOnly cacheEntryId with
            | Some summary -> 
                let dependencies =
                    node.Dependencies
                    |> Seq.choose (fun nodeId -> 
                        match restorables.TryGetValue nodeId with
                        | true, restorable -> Some restorable
                        | _ -> None)
                    |> List.ofSeq

                let callback() =
                    notification.NodeDownloading node
                    match cache.TryGetSummary allowRemoteCache cacheEntryId with
                    | Some summary ->
                        Log.Debug("{NodeId} restoring '{Project}/{Target}' from cache from {Hash}", node.Id, node.Project, node.Target, node.TargetHash)
                        match summary.Outputs with
                        | Some outputs ->
                            let files = IO.enumerateFiles outputs
                            IO.copyFiles projectDirectory outputs files |> ignore
                            api |> Option.iter (fun api -> api.UseArtifact node.ProjectHash node.TargetHash)
                        | _ -> ()
                        notification.NodeCompleted node TaskRequest.Restore true
                    | _ ->
                        notification.NodeCompleted node TaskRequest.Restore false
                        Errors.TerrabuildException.Raise($"Unable to download build output for {cacheEntryId} for node {node.Id}")

                let restorable = Restorable(callback, dependencies)
                restorables.TryAdd(node.Id, restorable) |> ignore
                TaskStatus.Success summary.EndedAt
            | _ ->
                TaskStatus.Failure (DateTime.UtcNow, $"Unable to download build output for {cacheEntryId} for node {node.Id}")


        let buildRequest, completionDate =
            if force then
                Log.Debug("{NodeId} must rebuild because force build requested", node.Id)
                TaskRequest.Build, buildNode DateTime.MaxValue
            elif maxCompletionChildren = DateTime.MaxValue then
                Log.Debug("{NodeId} must rebuild because child is rebuilding", node.Id)
                TaskRequest.Build, buildNode DateTime.MaxValue
            elif node.Cache <> Terrabuild.Extensibility.Cacheability.Never then
                let cacheEntryId = GraphDef.buildCacheKey node
                match tryGetSummaryOnly cacheEntryId with
                | Some summary ->
                    Log.Debug("{NodeId} has existing build summary", node.Id)
                    // task is younger than children
                    if summary.StartedAt < maxCompletionChildren then
                        Log.Debug("{NodeId} must rebuild because it is younger than child", node.Id)
                        TaskRequest.Build, buildNode DateTime.MaxValue
                    // task is failed and retry requested
                    elif retry && not summary.IsSuccessful then
                        Log.Debug("{NodeId} must rebuild because node is failed and retry requested", node.Id)
                        TaskRequest.Build, buildNode DateTime.MaxValue
                    // state is external - it's getting complex :-(
                    // UNDONE
                    elif checkState then // && (node.Cache &&& Terrabuild.Extensibility.Cacheability.External) <> Terrabuild.Extensibility.Cacheability.Never then
                        Log.Debug("{NodeId} is external, checking if state has changed", node.Id)
                        // first restore node because we want to have asset
                        // this **must** be ok since we were able to fetch metadata
                        let restoreCompletionStatus = restoreNode()
                        match restoreCompletionStatus with
                        | TaskStatus.Failure _ -> TaskRequest.Restore, restoreCompletionStatus
                        | TaskStatus.Success _ ->
                            // if retry is requested then completion date is either:
                            // - the local build with a new completionDate on changes
                            // - the local build with provided completionDate if no changes
                            Log.Debug("{NodeId} checking external state by building node again", node.Id)
                            let completionStatus = buildNode summary.EndedAt
                            match completionStatus with
                            | TaskStatus.Failure _ -> TaskRequest.Build, completionStatus
                            | TaskStatus.Success completionDate when summary.EndedAt = completionDate ->
                                // successfully validated restore so continue pretenting it's been restored
                                Log.Debug("{NodeId} state has not changed", node.Id)
                                TaskRequest.Restore, completionStatus
                            | _ ->
                                // changes have been detected so continue pretenting it's been rebuilt iif retry is requested
                                if retry then
                                    Log.Debug("{NodeId} state has changed, keeping changes", node.Id)
                                    TaskRequest.Build, completionStatus
                                else
                                    Log.Debug("{NodeId} mark node as failed since state has changed", node.Id)
                                    TaskRequest.Restore, TaskStatus.Failure (summary.EndedAt, "External state is no more valid. Rerun with retry.")
                    // task is cached
                    else
                        Log.Debug("{NodeId} is marked as used", node.Id)
                        TaskRequest.Restore, restoreNode()
                | _ ->
                    Log.Debug("{NodeId} must be build since no summary and required", node.Id)
                    TaskRequest.Build, buildNode DateTime.MaxValue
            else
                Log.Debug("{NodeId} is not cacheable", node.Id)
                TaskRequest.Build, buildNode DateTime.MaxValue

        buildRequest, completionDate


    let scheduledNodes = Concurrent.ConcurrentDictionary<string, bool>()
    let hub = Hub.Create(options.MaxConcurrency)
    let rec schedule nodeId =
        if scheduledNodes.TryAdd(nodeId, true) then
            let node = graph.Nodes[nodeId]
            let nodeComputed = hub.GetSignal<DateTime> nodeId

            // await dependencies
            let awaitedDependencies =
                node.Dependencies
                |> Seq.map (fun awaitedProjectId ->
                    schedule awaitedProjectId
                    hub.GetSignal<DateTime> awaitedProjectId)
                |> Array.ofSeq

            let onAllSignaled () =
                try
                    let maxCompletionChildren =
                        match awaitedDependencies with
                        | [| |] -> DateTime.MinValue
                        | _ -> awaitedDependencies |> Seq.maxBy (fun dep -> dep.Value) |> (fun dep -> dep.Value)

                    let buildRequest, completionStatus = processNode maxCompletionChildren node
                    Log.Debug("{NodeId} completed request {Request} with status {Status}", node.Id, buildRequest, completionStatus)
                    nodeResults.TryAdd(node.Id, (buildRequest, completionStatus)) |> ignore

                    match completionStatus with
                    | TaskStatus.Success completionDate ->
                        nodeComputed.Value <- completionDate
                        notification.NodeCompleted node buildRequest true
                    | _ ->
                        notification.NodeCompleted node buildRequest false
                with
                    exn ->
                        Log.Fatal(exn, "{NodeId} unexpected failure while building", node.Id)

                        nodeResults.TryAdd(node.Id, (TaskRequest.Build, TaskStatus.Failure (DateTime.UtcNow, exn.Message))) |> ignore
                        notification.NodeCompleted node TaskRequest.Build false

                        reraise()

            let awaitedSignals = awaitedDependencies |> Array.map (fun entry -> entry :> ISignal)
            hub.Subscribe awaitedSignals onAllSignaled

    graph.RootNodes |> Seq.iter schedule

    let status = hub.WaitCompletion()
    match status with
    | Status.Ok -> Log.Debug("Build successful")
    | Status.SubcriptionNotRaised projectId -> Log.Debug("Build failed: project {projectId} is not processed", projectId)
    | Status.SubscriptionError exn -> Log.Fatal(exn, "Build failed with exception")

    let headCommit = options.HeadCommit
    let branchOrTag = options.BranchOrTag

    let startedAt = options.StartedAt
    let endedAt = DateTime.UtcNow
    let buildDuration = endedAt - startedAt
    let totalDuration = endedAt - startedAt

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
                      Summary.StartedAt = startedAt
                      Summary.EndedAt = endedAt
                      Summary.BuildDuration = buildDuration
                      Summary.TotalDuration = totalDuration
                      Summary.IsSuccess = isSuccess
                      Summary.Targets = options.Targets
                      Summary.Nodes = nodeStatus }

    notification.BuildCompleted buildInfo

    buildInfo
