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
    Required: bool
    Batched: bool

    TargetHash: string
    CommandLines: Configuration.ContaineredActionBatch list
}

type Workspace = {
    Targets: string set
    Nodes: Map<string, Node>
    RootNodes: string set
}



let graph (graph: Workspace) =
    let projects =
        graph.Nodes.Values
        |> Seq.groupBy (fun x -> x.Project)
        |> Map.ofSeq
        |> Map.map (fun _ v -> v |> List.ofSeq)

    let colors =
        projects
        |> Map.map (fun k v ->
            let hash = Hash.sha256 k
            $"#{hash.Substring(0, 3)}")

    let mermaid = seq {
        "flowchart LR"
        $"classDef bold stroke:black,stroke-width:3px"

        // declare colors
        for (KeyValue(project, color)) in colors do
            $"classDef {project} fill:{color}"

        // nodes and arrows
        for (KeyValue(nodeId, node)) in graph.Nodes do
            let srcNode = graph.Nodes |> Map.find nodeId
            $"{srcNode.Hash}([{srcNode.Label}])"
            for dependency in node.Dependencies do
                let dstNode = graph.Nodes |> Map.find dependency
                $"{srcNode.Hash}([{srcNode.Label}]) --> {dstNode.Hash}([{dstNode.Label}])"

            $"class {srcNode.Hash} {srcNode.Project}"
            if graph.RootNodes |> Set.contains srcNode.Id then $"class {srcNode.Hash} bold"
    }

    mermaid




let create (configuration: Configuration.Workspace) (targets: string set) =
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
                    yield! target.Actions |> Seq.collect (fun batch -> 
                        batch.Actions |> Seq.map (fun cmd ->
                            $"{batch.Container} {cmd.Command} {cmd.Arguments}"))
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
                             TargetHash = target.Hash
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

    let rootNodes =
        configuration.ComputedProjectSelection |> Seq.collect (fun dependency ->
            targets |> Seq.collect (fun target -> buildTarget target dependency))
        |> Set

    { Targets = targets
      Nodes = allNodes |> Map.ofDict
      RootNodes = rootNodes }



let trim (configuration: Configuration.Workspace) (graph: Workspace) (cache: Cache.ICache)  (options: Configuration.Options) =
    let startedAt = DateTime.UtcNow

    let cacheMode =
        if configuration.SourceControl.CI then Cacheability.Always
        else Cacheability.Remote

    let shallRebuild (node: Node) =
        let useRemoteCache = Cacheability.Never <> (node.Cache &&& cacheMode)
        let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.Hash}"
        if options.Force || node.Cache = Cacheability.Never then
            true
        else
            let summary = cache.TryGetSummaryOnly useRemoteCache cacheEntryId
            match summary with
            | Some summary -> options.Retry && summary.Status <> Cache.TaskStatus.Success
            | _ -> true

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

    let hub = Hub.Create(options.MaxConcurrency)
    for (KeyValue(depNodeId, nodeIds)) in reversedDependencies do
        let nodeComputed = hub.CreateComputed<bool> depNodeId

        // await dependencies
        let awaitedDependencies =
            nodeIds
            |> Seq.map (fun awaitedProjectId -> hub.GetComputed<bool> awaitedProjectId)
            |> Array.ofSeq

        let awaitedSignals = awaitedDependencies |> Array.map (fun entry -> entry :> ISignal)
        hub.Subscribe awaitedSignals (fun () ->
            let node = graph.Nodes[depNodeId]
            let parentRequired =
                awaitedDependencies |> Seq.fold (fun acc dep -> acc || dep.Value) false
                || shallRebuild node

            let node = { node with Required = parentRequired }
            allNodes.TryAdd(depNodeId, node) |> ignore
            nodeComputed.Value <- parentRequired)

    let status = hub.WaitCompletion()
    match status with
    | Status.Ok -> ()
    | Status.SubcriptionNotRaised projectId -> TerrabuildException.Raise($"Node {projectId} is unknown")
    | Status.SubscriptionError exn -> TerrabuildException.Raise("Optimization error", exn)

    let endedAt = DateTime.UtcNow

    let trimDuration = endedAt - startedAt
    Log.Debug("Trim: {duration}", trimDuration)

    { graph with Nodes = allNodes |> Map.ofDict }





let optimize (configuration: Configuration.Workspace) (graph: Workspace) (cache: Cache.ICache)  (options: Configuration.Options) =
    let startedAt = DateTime.UtcNow

    let cacheMode =
        if configuration.SourceControl.CI then Cacheability.Always
        else Cacheability.Remote

    let shallRebuild (node: Node) =
        let useRemoteCache = Cacheability.Never <> (node.Cache &&& cacheMode)
        let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.Hash}"
        if options.Force || node.Cache = Cacheability.Never then
            true
        else
            let summary = cache.TryGetSummaryOnly useRemoteCache cacheEntryId
            match summary with
            | Some summary -> options.Retry && summary.Status <> Cache.TaskStatus.Success
            | _ -> true

    // map a node to a cluster
    let clusters = Concurrent.ConcurrentDictionary<string, string*string>()
    let computeCluster (node: Node) =
        let nodeTag, reason =
            // NOTE: if we are running log command we ensure idempotency
            //       otherwise we are seeking to optimize graph and if task is already built, do not batch build
            if (options.IsLog || node |> shallRebuild) |> not then
                $"{node.TargetHash}-{node.Id}", "not batchable/no build required"
            // node has no dependencies so try to link to existing tag (this will bootstrap the infection with same virus)
            else
                // check all actions are batchable
                let batchable =
                    node.Dependencies
                    |> Set.forall (fun dependency -> graph.Nodes[dependency].CommandLines |> List.forall (fun cmd -> cmd.BatchContext <> None))

                if batchable |> not then
                    $"{node.TargetHash}-{node.Id}", "not batchable"
                else
                    // collect tags from dependencies
                    // if they are the same then infect this node with children virus iif it's unique
                    // otherwise mutate the virus in new variant
                    let childrenTags =
                        node.Dependencies
                        |> Set.choose (fun dependency ->
                            let dependencyNode = graph.Nodes[dependency]
                            if dependencyNode |> shallRebuild then Some (clusters[dependency] |> fst, dependencyNode.TargetHash)
                            else None)
                        |> List.ofSeq

                    match childrenTags with
                    | [tag, hash] when hash = node.TargetHash ->
                        tag, "batchable/diffusion"
                    | [] ->
                        node.TargetHash, "batchable/no impacting dependencies"
                    | childrenTags ->
                        let tags = childrenTags |> List.map fst |> String.join "/"
                        $"{node.TargetHash}-{node.Id}", $"not batchable/multiple tags: {tags}"
        clusters.TryAdd(node.Id, (nodeTag, reason)) |> ignore
        Log.Debug("Node {node} ({label}) is assigned tag {tag} with reason {reason}", node.Id, node.Label, nodeTag, reason)


    // optimization is like building but instead of running actions
    // conceptually, it's inspired from virus infection :-)
    // this starts spontaneously with leaf nodes (patients 0) - same virus even they are not related (leaf nodes have no dependencies by definition)
    // then the infection flows to parents if they are compatible
    // if they are not compatible - the virus mutates and continue its propagation up to the root nodes
    // nodeTags dictionary holds a mapping of nodes to virus variant - they are our clusters

    let hub = Hub.Create(options.MaxConcurrency)
    for (KeyValue(nodeId, node)) in graph.Nodes do
        let nodeComputed = hub.CreateComputed<Node> nodeId

        // await dependencies
        let awaitedDependencies =
            node.Dependencies
            |> Seq.map (fun awaitedProjectId -> hub.GetComputed<Node> awaitedProjectId)
            |> Array.ofSeq

        let awaitedSignals = awaitedDependencies |> Array.map (fun entry -> entry :> ISignal)
        hub.Subscribe awaitedSignals (fun () ->
            computeCluster node
            nodeComputed.Value <- node)

    let status = hub.WaitCompletion()
    match status with
    | Status.Ok -> ()
    | Status.SubcriptionNotRaised projectId -> TerrabuildException.Raise($"Node {projectId} is unknown")
    | Status.SubscriptionError exn -> TerrabuildException.Raise("Optimization error", exn)

    // find a project for each cluster
    let clusterNodes =
        clusters
        |> Seq.map (fun kvp -> kvp.Value |> fst, kvp.Key)
        |> Seq.groupBy fst
        |> Map.ofSeq
        |> Map.map (fun _ nodeIds -> nodeIds |> Seq.map snd |> Set.ofSeq)

    // invoke __optimize__ function on each cluster
    let mutable graph = graph
    for (KeyValue(cluster, nodeIds)) in clusterNodes do
        let oneNodeId = nodeIds |> Seq.head
        let oneNode = graph.Nodes |> Map.find oneNodeId

        if nodeIds.Count > 1 then
            let nodes =
                nodeIds
                |> Seq.map (fun nodeId -> graph.Nodes |> Map.find nodeId)
                |> List.ofSeq

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
                        Some { action
                               with BatchContext = None
                                    Actions = actionBatch }
                    | _ -> None
                )

            let optimizedActions =
                target.Actions
                |> List.choose optimizeAction

            // did we optimize everything ?
            if optimizedActions.Length = target.Actions.Length then
                let projectList =
                    nodes
                    |> Seq.map (fun node -> node.Project)
                    |> String.join " "
                    |> String.cut 80

                let clusterNode = {
                    Node.Id = cluster
                    Node.Hash = clusterHash
                    Node.Project = $"batch/{cluster}"
                    Node.Target = oneNode.Target
                    Node.Label = $"batch-{oneNode.Target} {projectList}"
                    Node.Dependencies = clusterDependencies
                    ProjectHash = clusterHash
                    Outputs = Set.empty
                    Cache = oneNode.Cache
                    IsLeaf = oneNode.IsLeaf
                    Batched = false
                    Required = true

                    TargetHash = cluster
                    CommandLines = optimizedActions
                }

                // add cluster node to the graph
                graph <- { graph with Nodes = graph.Nodes |> Map.add cluster clusterNode }
                
                // patch each nodes to have a single dependency on the cluster
                for node in nodes do
                    let node = { node with
                                    Dependencies = Set.singleton cluster
                                    Label = $"post-{node.Target} {node.Project}"
                                    Batched = true
                                    CommandLines = List.Empty }
                    graph <- { graph with
                                    Nodes = graph.Nodes |> Map.add node.Id node }
            else
                Log.Debug("Failed to optimize cluster {cluster}", cluster)
        else
            Log.Debug("Cluster {cluster} has only 1 node", cluster)

    let endedAt = DateTime.UtcNow

    let optimizationDuration = endedAt - startedAt
    Log.Debug("Optimization: {duration}", optimizationDuration)

    let nodesToRun = graph.Nodes.Count
    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} {nodesToRun} tasks" |> Terminal.writeLine

    graph
