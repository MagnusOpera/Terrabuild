module Graph
open System
open System.Collections.Generic
open System.Collections.Concurrent
open Collections
open Terrabuild.Extensibility
open Serilog

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

    TargetHash: string
    CommandLines: Configuration.ContaineredActionBatch list
}

type WorkspaceGraph = {
    Targets: string set
    Nodes: Map<string, Node>
    RootNodes: string set
}

let buildGraph (wsConfig: Configuration.WorkspaceConfig) (targets: string set) =
    let processedNodes = ConcurrentDictionary<string, bool>()
    let allNodes = ConcurrentDictionary<string, Node>()

    let rec buildTarget targetName project =
        let nodeId = $"{project}:{targetName}"

        let processNode () =
            let projectConfig = wsConfig.Projects[project]

            // merge targets requirements
            let buildDependsOn =
                wsConfig.Targets
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
            let children =
                dependsOns
                |> Set.fold (fun acc dependsOn ->
                    let childDependencies =
                        match dependsOn with
                        | String.Regex "^\^(.+)$" [ parentDependsOn ] ->
                            projectConfig.Dependencies |> Set.collect (buildTarget parentDependsOn)
                        | _ ->
                            buildTarget dependsOn project
                    acc + childDependencies) Set.empty

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

                let hash = hashContent |> Hash.sha256list

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
                             TargetHash = target.Hash }
                if allNodes.TryAdd(nodeId, node) |> not then
                    failwith "Unexpected graph building race"
                Set.singleton nodeId
            | _ ->
                children

        if processedNodes.TryAdd(nodeId, true) then processNode()
        else Set.singleton nodeId

    let rootNodes =
        wsConfig.Dependencies |> Seq.collect (fun dependency ->
            targets |> Seq.collect (fun target -> buildTarget target dependency))
        |> Set

    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} {allNodes.Count} tasks to evaluate" |> Terminal.writeLine

    { Targets = targets
      Nodes = allNodes |> Map.ofDict
      RootNodes = rootNodes }



let graph (graph: WorkspaceGraph) =
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






let optimize (wsConfig: Configuration.WorkspaceConfig) (graph: WorkspaceGraph) (cache: Cache.ICache)  (options: Configuration.Options) =
    let startedAt = DateTime.UtcNow
    let mutable graph = graph

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
    let clusters = Concurrent.ConcurrentDictionary<string, string*string>()

    let cacheMode =
        if wsConfig.SourceControl.CI then Cacheability.Always
        else Cacheability.Remote

    let isCached (node: Node) =
        let useRemoteCache = Cacheability.Never <> (node.Cache &&& cacheMode)
        let cacheEntryId = $"{node.Project}/{node.Target}/{node.Hash}"
        if options.Force || node.Cache = Cacheability.Never then true
        else not <| cache.Exists useRemoteCache cacheEntryId

    // optimization is like building but instead of running actions
    // conceptually, it's inspired from virus infection :-)
    // this starts spontaneously with leaf nodes (patients 0) - same virus even they are not related (leaf nodes have no dependencies by definition)
    // then the infection flows to parents if they are compatible
    // if they are not compatible - the virus mutates and continue its propagation up to the root nodes
    // nodeTags dictionary holds a mapping of nodes to virus variant - they are our clusters
    let buildQueue = Exec.BuildQueue(1)
    let rec scheduleNode (nodeId: string) =
        let node = graph.Nodes[nodeId]

        let nodeTag, reason =
            // if not is already built then no batch build
            if node |> isCached |> not then
                $"{node.TargetHash}-{nodeId}", "no build required"
            // node has no dependencies so try to link to existing tag (this will bootstrap the infection with same virus)
            elif node.Dependencies = Set.empty then
                // printfn $"{node.Label} ==> assigned to cluster {node.TargetHash} since no dependencies"
                node.TargetHash, "no dependencies"
            else
                // check all actions are bacthable
                let batchable =
                    node.Dependencies
                    |> Seq.forall (fun dependency -> graph.Nodes[dependency].CommandLines |> List.forall (fun cmd -> cmd.BatchContext <> None))
               
                if batchable |> not then
                    $"{node.TargetHash}-{nodeId}", "not batchable"
                else
                    // collect tags from dependencies
                    // if they are the same then infect this node with children virus iif it's unique
                    // otherwise mutate the virus in new variant
                    let childrenTags =
                        node.Dependencies
                        |> Set.choose (fun dependency ->
                            let dependencyNode = graph.Nodes[dependency]
                            if dependencyNode |> isCached then Some (clusters[dependency] |> fst, dependencyNode.TargetHash)
                            else None)
                        |> List.ofSeq
                    match childrenTags with
                    | [tag, hash] when hash = node.TargetHash ->
                        tag, "diffusion"
                    | _ ->
                        $"{node.TargetHash}-{nodeId}", "multiple tags"
        clusters.TryAdd(nodeId, (nodeTag, reason)) |> ignore
        Log.Debug("Node {node} ({label}) is assigned tag {tag} with reason {reason}", nodeId, node.Label, nodeTag, reason)


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

    // find a project for each cluster
    let clusterNodes =
        clusters
        |> Seq.map (fun kvp -> kvp.Value |> fst, kvp.Key)
        |> Seq.groupBy fst
        |> Map.ofSeq
        |> Map.map (fun _ nodeIds -> nodeIds |> Seq.map snd |> Set.ofSeq)

    // invoke __optimize__ function on each cluster
    for (KeyValue(cluster, nodeIds)) in clusterNodes do
        let oneNodeId = nodeIds |> Seq.head
        let oneNode = graph.Nodes |> Map.find oneNodeId

        if nodeIds.Count > 1 then
            let nodes =
                nodeIds
                |> Seq.map (fun nodeId -> graph.Nodes |> Map.find nodeId)
                |> List.ofSeq

            // get hands on target
            let project = wsConfig.Projects |> Map.find oneNode.Project
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
                |> Hash.sha256list

            let optimizeAction (action: Configuration.ContaineredActionBatch) =
                action.BatchContext
                |> Option.bind (fun context ->
                    let optContext = {
                        OptimizeContext.Debug = options.Debug
                        OptimizeContext.CI = wsConfig.SourceControl.CI
                        OptimizeContext.BranchOrTag = wsConfig.SourceControl.BranchOrTag
                        OptimizeContext.ProjectPaths = projectPaths
                        OptimizeContext.TempDir = ".terrabuild"
                        OptimizeContext.NodeHash = clusterHash
                    }
                    let parameters = 
                        match context.Context with
                        | Terrabuild.Expressions.Value.Map map ->
                            map
                            |> Map.add "context" (Terrabuild.Expressions.Value.Object optContext)
                            |> Terrabuild.Expressions.Value.Map
                        | _ -> failwith "internal error"

                    let optCommand = $"__{context.Command}__"
                    let result = Extensions.invokeScriptMethod<Action list> optCommand parameters (Some context.Script)
                    match result with
                    | Extensions.InvocationResult.Success actionBatch ->
                        Some { 
                            Configuration.ContaineredActionBatch.BatchContext = None
                            Configuration.ContaineredActionBatch.Cache = action.Cache
                            Configuration.ContaineredActionBatch.Container = action.Container
                            Configuration.ContaineredActionBatch.Actions = actionBatch }
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
                                    CommandLines = List.Empty }
                    graph <- { graph with
                                    Nodes = graph.Nodes |> Map.add node.Id node }

    let endedAt = DateTime.UtcNow

    let optimizationDuration = endedAt - startedAt
    Log.Debug("Optimization: {duration}", optimizationDuration)

    graph
