module Graph
open System
open System.Collections.Concurrent
open Collections
open Terrabuild.Extensibility
open Serilog
open Errors
open Terrabuild.PubSub

type Paths = string set


type Node = {
    Id: string
    Hash: string
    Project: string
    Target: string
    Label: string
    Dependencies: string set
    ProjectHash: string
    Outputs: string set
    Cache: Cacheability
    IsLeaf: bool
    Forced: bool
    Required: bool
    BuildSummary: Cache.TargetSummary option
    Batched: bool

    Cluster: string
    CommandLines: Configuration.ContaineredActionBatch list
}

type Workspace = {
    Targets: string set
    Nodes: Map<string, Node>
    SelectedNodes: string set
}



let graph (graph: Workspace) =
    let clusterColors =
        graph.Nodes
        |> Seq.map (fun (KeyValue(nodeId, node)) ->
            let hash = Hash.sha256 node.Cluster
            node.Cluster, $"#{hash.Substring(0, 3)}")
        |> Map.ofSeq

    let clusters =
        graph.Nodes
        |> Seq.groupBy (fun (KeyValue(_, node)) -> node.Cluster)
        |> Map.ofSeq
        |> Map.map (fun _ v -> v |> Seq.map (fun kvp -> kvp.Value) |> List.ofSeq)

    let mermaid = [
        "flowchart LR"
        $"classDef forced stroke:red,stroke-width:3px"
        $"classDef required stroke:orange,stroke-width:3px"
        $"classDef selected stroke:black,stroke-width:3px"

        for (KeyValue(cluster, nodes)) in clusters do
            let clusterNode = nodes |> List.tryFind (fun node -> node.Id = cluster)
            let isCluster = clusterNode |> Option.isSome

            if isCluster then $"subgraph {cluster}[batch {clusterNode.Value.Target}]"

            let offset, nodes =
                if isCluster then "  ", nodes |> List.filter (fun node -> node.Id <> cluster)
                else "", nodes

            for node in nodes do
                $"{offset}{node.Hash}([{node.Label}])"

            if isCluster then
                "end"
                $"classDef cluster-{cluster} stroke:{clusterColors[cluster]},stroke-width:3px,fill:white,rx:10,ry:10"
                $"class {cluster} cluster-{cluster}"

            for srcNode in nodes do
                for dependency in srcNode.Dependencies do
                    if dependency <> cluster then
                        let dstNode = graph.Nodes |> Map.find dependency
                        $"{srcNode.Hash} --> {dstNode.Hash}"

                if srcNode.Forced then $"class {srcNode.Hash} forced"
                elif srcNode.Required then $"class {srcNode.Hash} required"
                elif graph.SelectedNodes |> Set.contains srcNode.Id then $"class {srcNode.Hash} selected"
    ]

    mermaid




let create (configuration: Configuration.Workspace) (targets: string set) (options: Configuration.Options) =
    $"{Ansi.Emojis.popcorn} Constructing graph" |> Terminal.writeLine

    let processedNodes = ConcurrentDictionary<string, bool>()
    let allNodes = ConcurrentDictionary<string, Node>()

    // first check all targets exist in WORKSPACE
    match targets |> Seq.tryFind (fun targetName -> configuration.Targets |> Map.containsKey targetName |> not) with
    | Some undefinedTarget -> TerrabuildException.Raise($"Target {undefinedTarget} is not defined in WORKSPACE")
    | _ -> ()

    let rec buildTarget targetName project =
        let nodeId = $"{project}:{targetName}"

        let processNode () =
            let projectConfig = configuration.Projects[project]

            // merge targets requirements
            let buildDependsOn =
                configuration.Targets
                |> Map.tryFind targetName
                |> Option.map (fun ct -> ct.DependsOn)
                |> Option.defaultValue Set.empty
            let projDependsOn =
                projectConfig.Targets
                |> Map.tryFind targetName
                |> Option.map (fun ct -> ct.DependsOn)
                |> Option.defaultValue Set.empty
            let dependsOns = buildDependsOn + projDependsOn

            // apply on each dependency
            let children, hasInternalDependencies =
                dependsOns
                |> Set.fold (fun (acc, hasInternalDependencies) dependsOn ->
                    let childDependencies, hasInternalDependencies =
                        match dependsOn with
                        | String.Regex "^\^(.+)$" [ parentDependsOn ] ->
                            projectConfig.Dependencies |> Set.collect (buildTarget parentDependsOn), hasInternalDependencies
                        | _ ->
                            buildTarget dependsOn project, true
                    acc + childDependencies, hasInternalDependencies) (Set.empty, false)

            // NOTE: a node is considered a leaf (within this project only) if the target has no internal dependencies detected
            let isLeaf = hasInternalDependencies |> not

            // only generate computation node - that is node that generate something
            // barrier nodes are just discarded and dependencies lift level up
            match projectConfig.Targets |> Map.tryFind targetName with
            | Some target ->
                let hashContent = [
                    yield! target.Variables |> Seq.map (fun kvp -> $"{kvp.Key} = {kvp.Value}")
                    yield! target.Actions |> Seq.map (fun batch -> batch.MetaCommand)
                    yield! children |> Seq.map (fun nodeId -> allNodes[nodeId].Hash)
                    yield projectConfig.Hash
                ]

                let hash = hashContent |> Hash.sha256strings

                // compute cacheability of this node
                let childrenCache =
                    children
                    |> Seq.fold (fun acc nodeId -> acc &&& allNodes[nodeId].Cache) Cacheability.Always

                let cache =
                    target.Actions
                    |> Seq.fold (fun acc cmd -> acc &&& cmd.Cache) childrenCache

                let isForced =
                    let isSelectedProject = configuration.SelectedProjects |> Set.contains project
                    let isSelectedTarget = targets |> Set.contains targetName
                    let forced = options.Force && isSelectedProject && isSelectedTarget
                    if forced then Log.Debug("{nodeId} must rebuild because force build is requested", nodeId)
                    forced

                let node = { Id = nodeId
                             Hash = hash
                             Project = project
                             Target = targetName
                             Label = $"{targetName} {project}"
                             CommandLines = target.Actions
                             Outputs = target.Outputs
                             Dependencies = children
                             ProjectHash = projectConfig.Hash
                             Cache = cache
                             IsLeaf = isLeaf
                             Required = false
                             Forced = isForced
                             BuildSummary = None
                             Cluster = target.Hash
                             Batched = false }

                if allNodes.TryAdd(nodeId, node) |> not then
                    TerrabuildException.Raise("Unexpected graph building race")
                Set.singleton nodeId
            | _ ->
                children

        if processedNodes.TryAdd(nodeId, true) then processNode()
        else
            match allNodes.TryGetValue(nodeId) with
            | true, _ -> Set.singleton nodeId
            | _ -> Set.empty

    let selectedNodes =
        configuration.SelectedProjects |> Seq.collect (fun dependency ->
            targets |> Seq.collect (fun target -> buildTarget target dependency))
        |> Set

    { Targets = targets
      Nodes = allNodes |> Map.ofDict
      SelectedNodes = selectedNodes }





let enforceConsistency (configuration: Configuration.Workspace) (graph: Workspace) (cache: Cache.ICache) (options: Configuration.Options) =
    let startedAt = DateTime.UtcNow

    let cacheMode =
        if configuration.SourceControl.CI then Cacheability.Always
        else Cacheability.Remote

    let allNodes = ConcurrentDictionary<string, Node>()
    let hub = Hub.Create(options.MaxConcurrency)
    for (KeyValue(nodeId, node)) in graph.Nodes do
        let nodeComputed = hub.CreateComputed<bool*DateTime> nodeId

        // await dependencies
        let awaitedDependencies =
            node.Dependencies
            |> Seq.map (fun awaitedProjectId -> hub.GetComputed<bool*DateTime> awaitedProjectId)
            |> Array.ofSeq

        let awaitedSignals = awaitedDependencies |> Array.map (fun entry -> entry :> ISignal)
        hub.Subscribe awaitedSignals (fun () ->

            // children can ask for a build
            let childrenRebuild, childrenLastBuild =
                awaitedDependencies |> Seq.fold (fun (childrenRebuild, childrenLastBuild) childComputed ->
                    let childRebuild, childLastBuild = childComputed.Value
                    let nodeRebuild = childrenRebuild || childRebuild
                    let nodeLastBuild = max childrenLastBuild childLastBuild
                    nodeRebuild, nodeLastBuild) (false, DateTime.MinValue)

            let summary, nodeRebuild, nodeLastBuild =
                if node.Forced then
                    Log.Debug("{nodeId} must rebuild because node is forced", nodeId)
                    None, true, DateTime.MaxValue

                elif childrenRebuild then
                    Log.Debug("{nodeId} must be rebuild because children must rebuild", nodeId)
                    None, true, DateTime.MaxValue

                else
                    let useRemoteCache = Cacheability.Never <> (node.Cache &&& cacheMode)
                    let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.Hash}"

                    // check first if it's possible to restore previously built state
                    let summary =
                        if node.Cache = Cacheability.Never then
                            Log.Debug("{nodeId} must rebuild because node never cached", nodeId)
                            None
                        else
                            // get task execution summary & take care of retrying failed tasks
                            match cache.TryGetSummary useRemoteCache cacheEntryId with
                            | Some summary when summary.Status = Cache.TaskStatus.Failure && options.Retry ->
                                Log.Debug("{nodeId} must rebuild because node is failed and retry requested", nodeId)
                                None
                            | Some summary ->
                                Log.Debug("{nodeId} has existing build summary", nodeId)
                                Some summary
                            | _ ->
                                Log.Debug("{nodeId} has no build summary", nodeId)
                                None

                    match summary with
                    | Some summary ->
                        if summary.StartedAt < childrenLastBuild then
                            Log.Debug("{nodeId} must rebuild because it is older than one of child", nodeId)
                            cache.Invalidate cacheEntryId
                            None, true, DateTime.MaxValue
                        else
                            (Some summary), false, summary.EndedAt
                    | _ ->
                        None, true, DateTime.MaxValue

            let node = { node with BuildSummary = summary
                                   Required = summary |> Option.isNone }
            allNodes.TryAdd(nodeId, node) |> ignore

            nodeComputed.Value <- (nodeRebuild, nodeLastBuild)
        )

    let status = hub.WaitCompletion()

    let endedAt = DateTime.UtcNow

    let trimDuration = endedAt - startedAt
    Log.Debug("Consistency: {duration}", trimDuration)
    { graph with Nodes = allNodes |> Map.ofDict }





let markRequired (graph: Workspace) (options: Configuration.Options) =
    let startedAt = DateTime.UtcNow

    let reversedDependencies =
        let reversedEdges =
            graph.Nodes
            |> Seq.collect (fun (KeyValue(nodeId, node)) -> node.Dependencies |> Seq.map (fun dependency -> dependency, nodeId))
            |> Seq.groupBy fst
            |> Seq.map (fun (k, v) -> k, v |> Seq.map snd |> List.ofSeq)
            |> Map.ofSeq

        graph.Nodes
        |> Map.map (fun _ _ -> [])
        |> Map.addMap reversedEdges

    let allNodes = ConcurrentDictionary<string, Node>()
    let hubOutputs = Hub.Create(options.MaxConcurrency)
    for (KeyValue(depNodeId, nodeIds)) in reversedDependencies do
        let nodeComputed = hubOutputs.CreateComputed<bool> depNodeId

        // await dependencies
        let awaitedDependencies =
            nodeIds
            |> Seq.map (fun awaitedProjectId -> hubOutputs.GetComputed<bool> awaitedProjectId)
            |> Array.ofSeq

        let awaitedSignals = awaitedDependencies |> Array.map (fun entry -> entry :> ISignal)
        hubOutputs.Subscribe awaitedSignals (fun () ->
            let node = graph.Nodes[depNodeId]
            let childRequired =
                awaitedDependencies |> Seq.fold (fun parentRequired dep -> parentRequired || dep.Value) (node.BuildSummary |> Option.isNone)
            let node = { node with Required = node.Forced || node.Required || childRequired }
            allNodes.TryAdd(depNodeId, node) |> ignore
            nodeComputed.Value <- childRequired)

    let status = hubOutputs.WaitCompletion()
    match status with
    | Status.Ok -> ()
    | Status.SubcriptionNotRaised projectId -> TerrabuildException.Raise($"Node {projectId} is unknown")
    | Status.SubscriptionError exn -> TerrabuildException.Raise("Optimization error", exn)

    let endedAt = DateTime.UtcNow

    let requiredDuration = endedAt - startedAt

    Log.Debug("Required: {duration}", requiredDuration)
    { graph with Nodes = allNodes |> Map.ofDict }



let optimize (configuration: Configuration.Workspace) (graph: Workspace) (options: Configuration.Options) =
    let startedAt = DateTime.UtcNow

    let shallRebuild (node: Node) =
        if node.Forced || node.Cache = Cacheability.Never then
            true
        else
            let summary = node.BuildSummary
            match summary with
            | Some summary -> options.Retry && summary.Status <> Cache.TaskStatus.Success
            | _ -> true

    let computeClusters remainingNodes =
        let clusters = Concurrent.ConcurrentDictionary<string, string set>()

        let hub = Hub.Create(options.MaxConcurrency)
        for nodeId in remainingNodes do
            let node  = graph.Nodes |> Map.find nodeId
            let nodeComputed = hub.CreateComputed<Node> nodeId

            let nodeDependencies = node.Dependencies |> Set.filter (fun dependency -> remainingNodes |> Set.contains dependency)

            // await dependencies
            let awaitedDependencies =
                nodeDependencies
                |> Seq.map (fun awaitedProjectId -> hub.GetComputed<Node> awaitedProjectId)
                |> Array.ofSeq

            let addToCluster (node: Node) =
                let add _ = Set.singleton node.Id
                let update _ nodes = nodes |> Set.add node.Id
                let declare, clusterId =
                    if node |> shallRebuild |> not then
                        Log.Debug("Node {node} does not need rebuild", node.Id)
                        true, node.Id
                    else
                        let isBatchable (node: Node) =
                            node.CommandLines
                            |> List.forall (fun cmd -> cmd.BatchContext <> None)

                        let batchable =
                            nodeDependencies
                            |> Set.add node.Id
                            |> Set.forall (fun dependency -> isBatchable graph.Nodes[dependency])

                        if batchable |> not then
                            Log.Debug("Node {node} is not batchable", node.Id)
                            true, node.Id
                        elif nodeDependencies = Set.empty || clusters.ContainsKey(node.Cluster) then
                            Log.Debug("Node {node} has joined cluster {cluster}", node.Id, node.Cluster)
                            true, node.Cluster
                        else
                            Log.Debug("Node {node} can't join any clusters", node.Id)
                            false, node.Id

                if declare then
                    lock clusters (fun () -> clusters.AddOrUpdate(clusterId, add, update) |> ignore)
                clusterId <> node.Id

            let awaitedSignals = awaitedDependencies |> Array.map (fun entry -> entry :> ISignal)
            hub.Subscribe awaitedSignals (fun () -> if addToCluster node then nodeComputed.Value <- node)

        let status = hub.WaitCompletion()
        status, clusters |> Map.ofDict

    let buildAllClusters() =
        let rec buildNextClusters availableNodes (status, clusters) =
            seq {
                if clusters |> Map.count = 0 then
                    match status with
                    | Status.Ok -> if availableNodes |> Set.count <> 0 then TerrabuildException.Raise($"Failed to optimize whole graph")
                    | Status.SubcriptionNotRaised projectId -> TerrabuildException.Raise($"Node {projectId} is unknown")
                    | Status.SubscriptionError exn -> TerrabuildException.Raise("Optimization error", exn)
                else
                    yield! clusters
                    let processedNodes = clusters.Values |> Set.collect id
                    let availableNodes = availableNodes - processedNodes
                    yield! availableNodes |> computeClusters |> buildNextClusters availableNodes
            }

        seq {
            let availableNodes = graph.Nodes.Keys |> Set.ofSeq
            yield! availableNodes |> computeClusters |> buildNextClusters availableNodes
        }

    let clusterNodes = buildAllClusters()

    // invoke __optimize__ function on each cluster
    let mutable graph = graph
    for (KeyValue(cluster, nodeIds)) in clusterNodes do
        let oneNodeId = nodeIds |> Seq.head
        let oneNode = graph.Nodes |> Map.find oneNodeId

        let nodes =
            nodeIds
            |> Seq.map (fun nodeId -> graph.Nodes |> Map.find nodeId)
            |> List.ofSeq

        let clusterNode =
            if nodeIds.Count > 1 then
                // get hands on target
                let project = configuration.Projects |> Map.find oneNode.Project
                let target = project.Targets |> Map.find oneNode.Target

                let projectPaths = nodes |> List.map (fun node -> node.Project)

                // cluster dependencies gather all nodeIds dependencies
                // nodes forming the cluster are removed (no-self dependencies)
                let clusterDependencies =
                    nodes
                    |> Seq.collect (fun node -> node.Dependencies) |> Set.ofSeq
                let clusterDependencies = clusterDependencies - nodeIds

                let clusterHash =
                    clusterDependencies
                    |> Seq.map (fun nodeId -> graph.Nodes[nodeId].Hash)
                    |> Hash.sha256strings

                let optimizeAction (action: Configuration.ContaineredActionBatch) =
                    action.BatchContext
                    |> Option.bind (fun context ->
                        let optContext = {
                            BatchContext.Debug = options.Debug
                            BatchContext.CI = configuration.SourceControl.CI
                            BatchContext.BranchOrTag = configuration.SourceControl.BranchOrTag
                            BatchContext.ProjectPaths = projectPaths
                            BatchContext.TempDir = ".terrabuild"
                            BatchContext.NodeHash = clusterHash
                        }
                        let parameters = 
                            match context.Context with
                            | Terrabuild.Expressions.Value.Map map ->
                                map
                                |> Map.add "context" (Terrabuild.Expressions.Value.Object optContext)
                                |> Terrabuild.Expressions.Value.Map
                            | _ -> TerrabuildException.Raise("internal error")

                        let optCommand = $"__{context.Command}__"
                        let result = Extensions.invokeScriptMethod<Action list> optCommand parameters (Some context.Script)
                        match result with
                        | Extensions.InvocationResult.Success actionBatch ->
                            Some { action with
                                    BatchContext = None
                                    Actions = actionBatch }
                        | _ -> None
                    )

                let optimizedActions =
                    target.Actions
                    |> List.choose optimizeAction

                // did we optimize everything ?
                if optimizedActions.Length = target.Actions.Length then
                    let cluster = $"cluster-{oneNode.Id}" |> Hash.sha256
                    let clusterNode = {
                        Node.Id = cluster
                        Node.Hash = clusterHash
                        Node.Project = $"batch/{cluster}"
                        Node.Target = oneNode.Target
                        Node.Label = $"batch-{oneNode.Target} {cluster}"
                        Node.Dependencies = clusterDependencies
                        ProjectHash = clusterHash
                        Outputs = Set.empty
                        Cache = oneNode.Cache
                        IsLeaf = oneNode.IsLeaf
                        Batched = false
                        Required = true
                        Forced = true
                        BuildSummary = None

                        Cluster = cluster
                        CommandLines = optimizedActions
                    }
                    Some clusterNode
                else
                    Log.Debug("Failed to optimize cluster {cluster}", cluster)
                    None
            else
                Log.Debug("Cluster {cluster} has only 1 node", cluster)
                None

        match clusterNode with
        | Some clusterNode -> graph <- { graph with Nodes = graph.Nodes |> Map.add clusterNode.Id clusterNode }
        | _ -> ()
            
        // patch each nodes to have a dependency on the cluster
        // but still keep dependencies because outputs must be recovered
        for node in nodes do
            let node =
                match clusterNode with
                | Some clusterNode ->
                    { node with
                        Dependencies = node.Dependencies |> Set.add clusterNode.Id
                        Batched = true
                        CommandLines = List.Empty
                        Cluster = clusterNode.Id }
                | _ ->
                    { node with Cluster = $"cluster-{node.Id}" |> Hash.sha256 }

            graph <- { graph with
                            Nodes = graph.Nodes |> Map.add node.Id node }

    let endedAt = DateTime.UtcNow

    let optimizationDuration = endedAt - startedAt
    Log.Debug("Optimization: {duration}", optimizationDuration)
    graph
