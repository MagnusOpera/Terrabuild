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


let run (options: Configuration.Options) (sourceControl: Contracts.ISourceControl) (cache: Cache.ICache) (api: Contracts.IApiClient option) (notification: IBuildNotification) (graph: GraphDef.Graph) =
    let targets = options.Targets |> String.join " "
    $"{Ansi.Emojis.rocket} Running targets [{targets}]" |> Terminal.writeLine

    let nodesToRun = graph.Nodes |> Map.filter (fun _ node -> node.IsRequired) |> Map.count
    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} {nodesToRun} tasks to run" |> Terminal.writeLine

    let startedAt = DateTime.UtcNow
    notification.BuildStarted graph
    let buildId =
        api |> Option.map (fun api -> api.BuildStart sourceControl.BranchOrTag sourceControl.HeadCommit options.Configuration options.Note options.Tag options.Targets options.Force options.Retry sourceControl.CI)
        |> Option.defaultValue ""

    let allowRemoteCache = options.LocalOnly |> not

    let workspaceDir = Environment.CurrentDirectory

    let containerInfos = Concurrent.ConcurrentDictionary<string, string>()

    let homeDir = cache.CreateHomeDir "container"

    let isBuildSuccess = function
        | NodeStatus.Success _ -> true
        | _ -> false

    // collect dependencies status
    let getDependencyStatus nodeId (node: GraphDef.Node) =
        let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"
        let nodeInfo = 
            { NodeInfo.Project = node.Project
              NodeInfo.Target = node.Target
              NodeInfo.ProjectHash = node.ProjectHash 
              NodeInfo.NodeHash = node.TargetHash }

        match cache.TryGetSummaryOnly false cacheEntryId with
        | Some summary -> 
            match summary.Status with
            | Cache.TaskStatus.Success -> NodeStatus.Success nodeInfo
            | Cache.TaskStatus.Failure -> NodeStatus.Failure nodeInfo
        | _ -> NodeStatus.Unfulfilled nodeInfo


    let processNode (node: GraphDef.Node) =
        let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"

        let projectDirectory =
            match node.Project with
            | FS.Directory projectDirectory -> projectDirectory
            | FS.File projectFile -> FS.parentDirectory projectFile
            | _ -> "."


        let buildNode() =
            notification.NodeBuilding node

            let cacheEntry = cache.GetEntry sourceControl.CI node.IsFirst cacheEntryId

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
                                match Exec.execCaptureOutput workspaceDir cmd args with
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
                        metaCommand, workspaceDir, cmd, args, operation.Container
                    | _ -> metaCommand, projectDirectory, operation.Command, operation.Arguments, operation.Container)

            let stepLogs = List<Cache.StepSummary>()
            let mutable lastExitCode = 0
            let mutable cmdLineIndex = 0
            let cmdFirstStartedAt = DateTime.UtcNow
            let mutable cmdLastEndedAt = cmdFirstStartedAt

            let logFile = cacheEntry.NextLogFile()
            let beforeFiles = IO.createSnapshot node.Outputs projectDirectory

            while cmdLineIndex < allCommands.Length && lastExitCode = 0 do
                let startedAt =
                    if cmdLineIndex > 0 then DateTime.UtcNow
                    else cmdFirstStartedAt
                let metaCommand, workDir, cmd, args, container = allCommands[cmdLineIndex]
                cmdLineIndex <- cmdLineIndex + 1

                Log.Debug("{Hash}: Running '{Command}' with '{Arguments}'", node.TargetHash, cmd, args)
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
                Log.Debug("{Hash}: Execution completed with '{Code}'", node.TargetHash, exitCode)

            let status =
                if lastExitCode = 0 then
                    Log.Debug("{Hash}: Marking as success", node.TargetHash)
                    Cache.TaskStatus.Success
                else
                    Log.Debug("{Hash}: Marking as failed", node.TargetHash)
                    Cache.TaskStatus.Failure

            let afterFiles = IO.createSnapshot node.Outputs projectDirectory

            // keep only new or modified files
            let newFiles = afterFiles - beforeFiles
            let outputs = IO.copyFiles cacheEntry.Outputs projectDirectory newFiles

            let summary = { Cache.TargetSummary.Project = node.Project
                            Cache.TargetSummary.Target = node.Target
                            Cache.TargetSummary.Steps = [ stepLogs |> List.ofSeq ]
                            Cache.TargetSummary.Outputs = outputs
                            Cache.TargetSummary.Status = status
                            Cache.TargetSummary.StartedAt = cmdFirstStartedAt
                            Cache.TargetSummary.EndedAt = cmdLastEndedAt
                            Cache.TargetSummary.Origin = Cache.Origin.Local }
            cacheEntry.CompleteLogFile summary

            if node.IsLast then
                notification.NodeUploading node

                // create an archive with new files
                Log.Debug("{Hash}: Building '{Project}/{Target}'", node.TargetHash, node.Project, node.Target)
                let cacheEntry = cache.GetEntry sourceControl.CI false cacheEntryId
                let files, size = cacheEntry.Complete()
                api |> Option.iter (fun api -> api.BuildAddArtifact buildId node.Project node.Target node.ProjectHash node.TargetHash files size true)
                notification.NodeCompleted node (node.IsForced |> not) true

            if lastExitCode <> 0 then TerrabuildException.Raise("Build failure")

        let restoreNode () =
            notification.NodeDownloading node
            let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"
            match cache.TryGetSummary allowRemoteCache cacheEntryId with
            | Some summary ->
                Log.Debug("{Hash}: Restoring '{Project}/{Target}' from cache", node.TargetHash, node.Project, node.Target)
                match summary.Outputs with
                | Some outputs ->
                    let files = IO.enumerateFiles outputs
                    IO.copyFiles projectDirectory outputs files |> ignore
                | _ -> ()
            | _ -> TerrabuildException.Raise("Unable to download build output for {cacheEntryId}")

        try
            if node.IsForced then buildNode()
            else restoreNode()
            notification.NodeCompleted node (node.IsForced |> not) true
        with
            | exn ->
                Log.Fatal(exn, "Build failed with error")
                let cacheEntry = cache.GetEntry sourceControl.CI false cacheEntryId
                let files, size = cacheEntry.Complete()
                api |> Option.iter (fun api -> api.BuildAddArtifact buildId node.Project node.Target node.ProjectHash node.TargetHash files size false)            
                notification.NodeCompleted node (node.IsForced |> not) false
                reraise()


    let hub = Hub.Create(options.MaxConcurrency)
    let requiredNodes = graph.Nodes |> Map.filter (fun _ n -> n.IsRequired)
    for (KeyValue(nodeId, node)) in requiredNodes do
        let nodeComputed = hub.CreateComputed<GraphDef.Node> nodeId

        // await dependencies
        let awaitedDependencies =
            node.Dependencies
            |> Seq.map (fun awaitedProjectId -> hub.GetComputed<GraphDef.Node> awaitedProjectId)
            |> Array.ofSeq

        let awaitedSignals = awaitedDependencies |> Array.map (fun entry -> entry :> ISignal)
        hub.Subscribe awaitedSignals (fun () ->
            processNode node
            nodeComputed.Value <- node)

    let status = hub.WaitCompletion()
    match status with
    | Status.Ok -> ()
    | Status.SubcriptionNotRaised projectId -> TerrabuildException.Raise($"Project {projectId} is unknown")
    | Status.SubscriptionError exn -> TerrabuildException.Raise("Build error", exn)

    let headCommit = sourceControl.HeadCommit
    let branchOrTag = sourceControl.BranchOrTag

    // nodes that were considered for the whole requested build
    let buildNodes =
        graph.Nodes
        |> Map.filter (fun nodeId node -> node.IsForced)

    // status of nodes to build
    let buildNodesStatus =
        graph.RootNodes
        |> Seq.map (fun depId -> depId, graph.Nodes[depId])
        |> Map.ofSeq
        |> Map.map getDependencyStatus
        |> Map.values
        |> Set.ofSeq

    let endedAt = DateTime.UtcNow
    let buildDuration = endedAt - startedAt
    let totalDuration = endedAt - options.StartedAt

    let status =
        let isSuccess = buildNodesStatus |> Seq.forall isBuildSuccess
        if isSuccess then Status.Success
        else Status.Failure

    let buildInfo = { Summary.Commit = headCommit
                      Summary.BranchOrTag = branchOrTag
                      Summary.StartedAt = options.StartedAt
                      Summary.EndedAt = endedAt
                      Summary.BuildDuration = buildDuration
                      Summary.TotalDuration = totalDuration
                      Summary.Status = status
                      Summary.Targets = options.Targets
                      Summary.Nodes = graph.Nodes |> Map.keys |> Set.ofSeq
                      Summary.RequiredNodes = requiredNodes |> Map.keys |> Set.ofSeq
                      Summary.BuildNodes = buildNodes |> Map.keys |> Set.ofSeq
                      Summary.BuildNodesStatus = buildNodesStatus  }

    notification.BuildCompleted buildInfo
    api |> Option.iter (fun api -> api.BuildComplete buildId (status = Status.Success))

    buildInfo
