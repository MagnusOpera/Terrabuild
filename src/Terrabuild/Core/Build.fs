module Build
open System
open System.Collections.Generic
open Collections
open Serilog
open Terrabuild.Extensibility
open Terrabuild.PubSub
open Graph

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

    abstract BuildStarted: graph:Graph.Workspace -> unit
    abstract BuildCompleted: summary:Summary -> unit

    abstract NodeScheduled: node:Graph.Node -> unit
    abstract NodeDownloading: node:Graph.Node -> unit
    abstract NodeBuilding: node:Graph.Node -> unit
    abstract NodeUploading: node:Graph.Node -> unit
    abstract NodeCompleted: node:Graph.Node -> restored: bool -> summary:Cache.TargetSummary -> unit


let run (configuration: Configuration.Workspace) (graph: Graph.Workspace) (cache: Cache.ICache) (api: Contracts.IApiClient option) (notification: IBuildNotification) (options: Configuration.Options) =
    let targets = graph.Targets |> String.join ","
    let targetLabel = if graph.Targets.Count > 1 then "targets" else "target"
    $"{Ansi.Emojis.rocket} Running {targetLabel} {targets}" |> Terminal.writeLine

    let nodesToRun = graph.Nodes |> Map.filter (fun nodeId node -> node.Required) |> Map.count
    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} {nodesToRun} tasks to run" |> Terminal.writeLine

    let startedAt = DateTime.UtcNow
    notification.BuildStarted graph
    let buildId =
        api |> Option.map (fun api -> api.BuildStart configuration.SourceControl.BranchOrTag configuration.SourceControl.HeadCommit configuration.Configuration configuration.Note configuration.Tag graph.Targets options.Force options.Retry configuration.SourceControl.CI)
        |> Option.defaultValue ""

    let workspaceDir = Environment.CurrentDirectory

    let containerInfos = Concurrent.ConcurrentDictionary<string, string>()

    let isBuildSuccess = function
        | NodeStatus.Success _ -> true
        | _ -> false

    // collect dependencies status
    let getDependencyStatus nodeId node =
        let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.Hash}"
        let nodeInfo = 
            { NodeInfo.Project = node.Project
              NodeInfo.Target = node.Target
              NodeInfo.NodeHash = node.Hash
              NodeInfo.ProjectHash = node.ProjectHash }

        match cache.TryGetSummaryOnly false cacheEntryId with
        | Some summary -> 
            match summary.Status with
            | Cache.TaskStatus.Success -> NodeStatus.Success nodeInfo
            | Cache.TaskStatus.Failure -> NodeStatus.Failure nodeInfo
        | _ -> NodeStatus.Unfulfilled nodeInfo


    let buildNode (node: Graph.Node) =
        let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.Hash}"

        notification.NodeDownloading node

        let projectDirectory =
            match node.Project with
            | FS.Directory projectDirectory -> projectDirectory
            | FS.File projectFile -> FS.parentDirectory projectFile
            | _ -> "."

        // check first if it's possible to restore previously built state
        let summary = node.BuildSummary

        match summary with
        | Some summary ->
            Log.Debug("{Hash}: Restoring '{Project}/{Target}' from cache", node.Hash, node.Project, node.Target)
            match summary.Outputs with
            | Some outputs ->
                let files = IO.enumerateFiles outputs
                IO.copyFiles projectDirectory outputs files |> ignore
            | _ -> ()
            true, summary

        | _ ->
            Log.Debug("{Hash}: Building '{Project}/{Target}'", node.Hash, node.Project, node.Target)
            let cacheEntry = cache.CreateEntry configuration.SourceControl.CI cacheEntryId
            notification.NodeBuilding node

            // NOTE:
            //  we use ProjectHash here because it's interesting from a cache perspective
            //  some binaries could have been cached in homedir, let's reuse them if available
            let homeDir = cache.CreateHomeDir "global"

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
                            if batch.Actions.Length > 1 then $"{batch.MetaCommand} ({index+1}/{batch.Actions.Length})"
                            else batch.MetaCommand

                        match batch.Container, options.NoContainer with
                        | Some container, false ->
                            let whoami = getContainerUser container
                            let envs =
                                batch.ContainerVariables
                                |> Seq.map (fun var -> $"-e {var}")
                                |> String.join " "
                            let args = $"run --rm --net=host --name {node.Hash} -v /var/run/docker.sock:/var/run/docker.sock -v {homeDir}:/{whoami} -v {wsDir}:/terrabuild -w /terrabuild/{projectDirectory} --entrypoint {commandLine.Command} {envs} {container} {commandLine.Arguments}"
                            metaCommand, workspaceDir, cmd, args, batch.Container
                        | _ -> metaCommand, projectDirectory, commandLine.Command, commandLine.Arguments, batch.Container))

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
                            Cache.TargetSummary.EndedAt = cmdLastEndedAt
                            Cache.TargetSummary.Origin = Cache.Origin.Local }
            let files, size = cacheEntry.Complete summary
            api |> Option.iter (fun api -> api.BuildAddArtifact buildId node.Project node.Target node.ProjectHash node.Hash files size (status = Cache.TaskStatus.Success))
            false, summary

    let hub = Hub.Create(options.MaxConcurrency)
    let requiredNodes = graph.Nodes |> Map.filter (fun _ n -> n.Required)
    for (KeyValue(nodeId, node)) in requiredNodes do
        let nodeComputed = hub.CreateComputed<Node> nodeId

        // await dependencies
        let awaitedDependencies =
            node.Dependencies
            |> Seq.map (fun awaitedProjectId -> hub.GetComputed<Node> awaitedProjectId)
            |> Array.ofSeq

        let awaitedSignals = awaitedDependencies |> Array.map (fun entry -> entry :> ISignal)
        hub.Subscribe awaitedSignals (fun () ->
            let restored, summary = buildNode node
            notification.NodeCompleted node restored summary

            if summary.Status = Cache.TaskStatus.Success then
                nodeComputed.Value <- node
        )

    let status = hub.WaitCompletion()

    let headCommit = configuration.SourceControl.HeadCommit
    let branchOrTag = configuration.SourceControl.BranchOrTag

    // nodes that were considered for the whole requested build
    let buildNodes =
        graph.Nodes
        |> Map.filter (fun nodeId node ->
            node.Required || node.BuildSummary |> Option.isSome)

    // status of nodes to build
    let buildNodesStatus =
        buildNodes
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
                      Summary.Targets = graph.Targets
                      Summary.Nodes = graph.Nodes |> Map.keys |> Set.ofSeq
                      Summary.RequiredNodes = requiredNodes |> Map.keys |> Set.ofSeq
                      Summary.BuildNodes = buildNodes |> Map.keys |> Set.ofSeq
                      Summary.BuildNodesStatus = buildNodesStatus  }

    notification.BuildCompleted buildInfo
    api |> Option.iter (fun api -> api.BuildComplete buildId (status = Status.Success))

    buildInfo
