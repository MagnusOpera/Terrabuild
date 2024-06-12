module Graph
open System
open System.Collections.Generic
open System.Collections.Concurrent
open Collections
open Terrabuild.Extensibility
open Serilog
open Errors

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

    let nodesToRun = allNodes.Count
    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} {nodesToRun} tasks" |> Terminal.writeLine

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

    let mutable allNodes = Map.empty

    let rec skipNode parentRequired nodeId =
        let node = graph.Nodes[nodeId]
        let processNode () =
            let required = parentRequired || shallRebuild node
            let node = { node with Required = node.Required || required }
            allNodes <- allNodes |>Map.add nodeId node

            node.Dependencies |> Seq.iter (skipNode required) 

        processNode()

    // compute first incoming edges
    let reverseIncomings =
        graph.Nodes
        |> Map.map (fun _ _ -> List<string>())
    for KeyValue(nodeId, node) in graph.Nodes do
        for dependency in node.Dependencies do
            reverseIncomings[dependency].Add(nodeId)

    let rootNodes =
        reverseIncomings
        |> Map.filter (fun _ v -> v.Count = 0)
        |> Map.keys
        |> List.ofSeq

    rootNodes |> List.iter (skipNode false)

    let endedAt = DateTime.UtcNow

    let trimDuration = endedAt - startedAt
    Log.Debug("Trim: {duration}", trimDuration)

    { graph with Nodes = allNodes }





let optimize (configuration: Configuration.Workspace) (graph: Workspace) (cache: Cache.ICache)  (options: Configuration.Options) =
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
    let buildQueue = Exec.BuildQueue(1)
    let rec queueAction (nodeId: string) =
        let node = graph.Nodes[nodeId]
        computeCluster node

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

    graph
