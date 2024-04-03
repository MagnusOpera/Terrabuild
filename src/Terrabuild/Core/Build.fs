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
    TotalDuration: TimeSpan
    BuildDuration: TimeSpan
    Status: BuildStatus
    Targets: string set
    RootNodes: NodeBuildStatus set
}


[<RequireQualifiedAccess>]
type ContaineredBulkActionBatch = {
    Container: string option
    Actions: Action list
}


[<RequireQualifiedAccess>]
type BulkNode = {
    Id: string
    Dependencies: string set
    Nodes: string set
    CommandLines: ContaineredBulkActionBatch list
}

[<RequireQualifiedAccess>]
type BuildNode =
    | Node of Graph.Node
    | BulkNode of BulkNode


type WorkspaceBuild = {
    Targets: string set
    Nodes: Map<string, BuildNode>
    RootNodes: string set
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
    let startedAt = DateTime.UtcNow

    let containerInfos = Concurrent.ConcurrentDictionary<string, string>()

    let cacheMode =
        if workspaceConfig.SourceControl.CI then Cacheability.Always
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
                if options.Force || node.Cache = Cacheability.Never then None
                else
                    // determine if step node can be reused or not
                    let useRemoteCache = Cacheability.Never <> (node.Cache &&& cacheMode)

                    // get task execution summary & take care of retrying failed tasks
                    match cache.TryGetSummary useRemoteCache cacheEntryId with
                    | Some summary when summary.Status = Cache.TaskStatus.Failure && options.Retry -> None
                    | Some summary -> Some summary
                    | _ -> None

            // clean outputs if leaf node (otherwise outputs are layered on top of previous ones)
            if node.IsLeaf then
                IO.enumerateFilesMatch node.Outputs projectDirectory
                |> Seq.iter IO.deleteAny

            match summary with
            | Some summary ->
                Log.Debug("{Hash}: Restoring '{Project}/{Target}' from cache", node.Hash, node.Project, node.Target)
                match summary.Outputs with
                | Some outputs ->
                    let files = IO.enumerateFiles outputs
                    IO.copyFiles projectDirectory outputs files |> ignore
                | _ -> ()
                Some summary

            | _ ->
                Log.Debug("{Hash}: Building '{Project}/{Target}'", node.Hash, node.Project, node.Target)
                let cacheEntry = cache.CreateEntry workspaceConfig.SourceControl.CI cacheEntryId
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
                            let wsDir = IO.combinePath Environment.CurrentDirectory workspaceConfig.Directory

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
                                        match Exec.execCaptureOutput workspaceConfig.Directory cmd args with
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
                                let args = $"run --rm --entrypoint {commandLine.Command} --name {node.Hash} -v /var/run/docker.sock:/var/run/docker.sock -v {homeDir}:/{whoami} -v {wsDir}:/terrabuild -w /terrabuild/{node.Project} {container} {commandLine.Arguments}"
                                workspaceConfig.Directory, cmd, args, batch.Container))


                let beforeFiles = FileSystem.createSnapshot projectDirectory node.Outputs

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
                let afterFiles = FileSystem.createSnapshot projectDirectory node.Outputs

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
    let buildDuration = endedAt - startedAt
    let totalDuration = endedAt - options.StartedAt

    let status =
        let isSuccess = dependencies |> Seq.forall isBuildSuccess
        if isSuccess then BuildStatus.Success
        else BuildStatus.Failure

    let buildInfo = { BuildSummary.Commit = headCommit
                      BuildSummary.BranchOrTag = branchOrTag
                      BuildSummary.StartedAt = options.StartedAt
                      BuildSummary.EndedAt = endedAt
                      BuildSummary.BuildDuration = buildDuration
                      BuildSummary.TotalDuration = totalDuration
                      BuildSummary.Status = status
                      BuildSummary.Targets = graph.Targets
                      BuildSummary.RootNodes = dependencies }
    notification.BuildCompleted buildInfo
    buildInfo










let optimizeGraph (wsConfig: Configuration.WorkspaceConfig) (options: Configuration.Options) (graph: Graph.WorkspaceGraph) =
    let startedAt = DateTime.UtcNow

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

    // map a node to a cluster
    let clusters = Concurrent.ConcurrentDictionary<string, string>()

    // optimization is like building but instead of running actions
    // pragmatically, it's inspired from virus infection :-)
    // this starts with leaf nodes (patients 0) - but with same virus even they are not related (leaf nodes have no dependencies by definition)
    // then the infection propagates to parents if they are compatible
    // if they are not compatible - the virus mutates and continue its propagation up to the root nodes
    // nodeTags dictionary holds a mapping of nodes to virus variant - they are our clusters
    let buildQueue = Exec.BuildQueue(1)
    let rec scheduleNode (nodeId: string) =
        let node = graph.Nodes[nodeId]

        let nodeTag =
            // node has no dependencies so try to link to existing tag (this will bootstrap the infection with same virus)
            if node.Dependencies = Set.empty then
                node.TargetHash
            else
                // collect tags from dependencies
                // if they are the same then infect this node with children virus iif it's unique
                // otherwise mutate the virus in new variant
                let childrenTags =
                    node.Dependencies
                    |> Set.map (fun dependency -> (clusters[dependency], graph.Nodes[dependency].TargetHash))
                    |> List.ofSeq
                match childrenTags with
                | [tag, hash] when hash = node.TargetHash -> tag
                | _ -> $"{node.TargetHash}-{nodeId}"
        clusters.TryAdd(nodeId, nodeTag) |> ignore


        let buildAction () = 
            // schedule children nodes if ready
            let triggers = reverseIncomings[nodeId]                
            for trigger in triggers do
                let newValue = System.Threading.Interlocked.Decrement(readyNodes[trigger])
                if newValue = 0 then
                    readyNodes[trigger].Value <- -1 // mark node as scheduled
                    scheduleNode trigger

        buildQueue.Enqueue buildAction


    readyNodes
    |> Map.filter (fun _ value -> value.Value = 0)
    |> Map.iter (fun key _ -> scheduleNode key)

    // wait only if we have something to do
    buildQueue.WaitCompletion()

    let endedAt = DateTime.UtcNow
    let optimizationDuration = endedAt - startedAt
    printfn $"Optimization = {optimizationDuration}"

    // find a project for each cluster
    let clusterNodes =
        clusters
        |> Seq.map (fun kvp -> kvp.Value, kvp.Key)
        |> Seq.groupBy fst
        |> Map.ofSeq
        |> Map.map (fun _ nodeIds -> nodeIds |> Seq.map snd |> Set.ofSeq)

    // invoke __optimize__ function on each cluster
    let mutable optimizedClusters = Map.empty
    for (KeyValue(cluster, nodeIds)) in clusterNodes do
        let oneNodeId = nodeIds |> Seq.head
        let oneNode = graph.Nodes |> Map.find oneNodeId

        if nodeIds.Count = 1 then
            // rewrite dependencies to clusters
            let dependencies =
                oneNode.Dependencies
                |> Set.map (fun dependencyId -> clusters[dependencyId])
            let oneNode = { oneNode
                            with Dependencies = dependencies }
            optimizedClusters <- optimizedClusters |> Map.add oneNodeId (BuildNode.Node oneNode)
        else
            printfn $"Optimizing cluster {cluster}"
            let nodes = 
                nodeIds
                |> Seq.map (fun nodeId -> graph.Nodes |> Map.find nodeId)
                |> List.ofSeq
    
            // get hands on target
            let project = wsConfig.Projects |> Map.find oneNode.Project
            let target = project.Targets |> Map.find oneNode.Target

            let projectPaths = nodes |> List.map (fun node -> IO.combinePath wsConfig.Directory node.Project)

            let optimizeAction (action: Configuration.ContaineredActionBatch) =
                action.BulkContext
                |> Option.bind (fun context ->
                    let optContext = {
                        OptimizeContext.Debug = options.Debug
                        OptimizeContext.CI = wsConfig.SourceControl.CI
                        OptimizeContext.BranchOrTag = wsConfig.SourceControl.BranchOrTag
                        OptimizeContext.ProjectPaths = projectPaths
                    }
                    let parameters = 
                        match context.Context with
                        | Terrabuild.Expressions.Value.Map map ->
                            map
                            |> Map.add "context" (Terrabuild.Expressions.Value.Object optContext)
                            |> Terrabuild.Expressions.Value.Map
                        | _ -> failwith "internal error"

                    let optCommand = $"bulk_{context.Command}"
                    let result = Extensions.invokeScriptMethod<Action list> optCommand parameters (Some context.Script)
                    match result with
                    | Extensions.InvocationResult.Success actionBatch ->
                        let bulkActionBatch = {
                            ContaineredBulkActionBatch.Actions = actionBatch
                            ContaineredBulkActionBatch.Container = action.Container
                        }
                        Some bulkActionBatch
                    | _ -> None
                )

            let optimizedActions =
                target.Actions
                |> List.choose optimizeAction

            // did we optimize everything ?
            if optimizedActions.Length = target.Actions.Length then
                // compute new dependencies (remap to cluster)
                let dependencies =
                    nodes
                    |> Seq.collect (fun node -> node.Dependencies) |> Set.ofSeq
                    |> Set.map (fun dependencyId -> clusters[dependencyId])
                let bulkNode = { BulkNode.Dependencies = dependencies
                                 BulkNode.Id = cluster
                                 BulkNode.CommandLines = optimizedActions
                                 BulkNode.Nodes = nodeIds }
                optimizedClusters <- optimizedClusters |> Map.add cluster (BuildNode.BulkNode bulkNode)


    let rootNodes =
        graph.RootNodes
        |> Set.map (fun nodeId -> clusters[nodeId])

    let buildGraph = {
        WorkspaceBuild.Targets = graph.Targets
        WorkspaceBuild.Nodes = optimizedClusters
        WorkspaceBuild.RootNodes = rootNodes }

    buildGraph

    // printfn $"clusters = {clusters.Count}"
    // for (KeyValue(cluster, nodes)) in clusterNodes do
    //     printfn $"{cluster} => {nodes.Count}"

    // printfn $"Optimized clusters = {optimizedClusters.Count}"
    // for (KeyValue(cluster, actions)) in optimizedClusters do
    //     printfn $"{cluster} => {actions}"
