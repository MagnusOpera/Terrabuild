module Graph
open System
open System.Collections.Generic
open System.Collections.Concurrent
open Collections
open Terrabuild.Extensibility
open Terrabuild.Expressions

type Paths = string set


type Node = {
    Id: string
    Hash: string
    Project: string
    Target: string
    Dependencies: string set
    ProjectHash: string
    Variables: Map<string, string>
    Outputs: string set
    Cache: Cacheability
    IsLeaf: bool

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
            let children, hasInternalDependencies =
                let mutable children = Set.empty
                let mutable hasInternalDependencies = false

                for dependsOn in dependsOns do
                    let childDependencies =
                        match dependsOn with
                        | String.Regex "^\^([a-zA-Z][_a-zA-Z0-9]+)$" [ parentDependsOn ] ->
                            projectConfig.Dependencies
                            |> Seq.collect (buildTarget parentDependsOn)
                        | _ ->
                            hasInternalDependencies <- true
                            buildTarget dependsOn project
                    children <- children + (childDependencies |> Set)
                children, hasInternalDependencies

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
                             Variables = target.Variables
                             CommandLines = target.Actions
                             Outputs = projectConfig.Outputs
                             Dependencies = children
                             IsLeaf = isLeaf
                             ProjectHash = projectConfig.Hash
                             Cache = cache
                             TargetHash = target.Hash }
                if allNodes.TryAdd(nodeId, node) |> not then
                    failwith "Unexpected graph building race"
                [ nodeId ]
            | _ ->
                children |> List.ofSeq

        if processedNodes.TryAdd(nodeId, true) then processNode()
        else [ nodeId ]

    let rootNodes =
        wsConfig.Dependencies |> Seq.collect (fun dependency ->
            targets |> Seq.collect (fun target -> buildTarget target dependency))
        |> Set

    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} {allNodes.Count} tasks to evaluate" |> Terminal.writeLine

    { Targets = targets
      Nodes = allNodes |> Map.ofDict
      RootNodes = rootNodes }


let optimize (graph: WorkspaceGraph) =
    let partitions =
        graph.Nodes.Values
        |> Seq.groupBy (fun x -> x.CommandLines)

    printfn $"nodes = {graph.Nodes.Count}"
    printfn $"partition = {partitions |> Seq.length}"
    for (key, values) in partitions do
        printfn $"{key} => {values |> Seq.length}"
        printfn ""


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
            for dependency in node.Dependencies do
                let dstNode = graph.Nodes |> Map.find dependency
                $"{srcNode.Hash}([{srcNode.Id}]) --> {dstNode.Hash}([{dstNode.Id}])"

            $"class {srcNode.Hash} {srcNode.Project}"
            if graph.RootNodes |> Set.contains srcNode.Id then $"class {srcNode.Hash} bold"
    }

    mermaid







let optimizeGraph (wsConfig: Configuration.WorkspaceConfig) (options: Configuration.Options) (graph: WorkspaceGraph) =
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
        |> Map.filter (fun _ v -> v |> Seq.length > 1)
        |> Map.map (fun _ nodeIds -> nodeIds |> Seq.map snd |> Set.ofSeq)

    // invoke __optimize__ function on each cluster
    let mutable optimizedClusters = Map.empty
    for (KeyValue(cluster, nodeIds)) in clusterNodes do
        printfn $"Optimizing cluster {cluster}"
        let oneNodeId = nodeIds |> Seq.head
        let oneNode = graph.Nodes |> Map.find oneNodeId
 
        // get hands on target
        let project = wsConfig.Projects |> Map.find oneNode.Project
        let target = project.Targets |> Map.find oneNode.Target

        let projectPaths =
            nodeIds
            |> Seq.map (fun nodeId ->
                let node = graph.Nodes |> Map.find nodeId
                IO.combinePath wsConfig.Directory node.Project)
            |> List.ofSeq

        let optimizeAction (action: Configuration.ContaineredActionBatch) =
            action.BulkContext
            |> Option.map (fun context ->
                let optContext = {
                    OptimizeContext.Debug = options.Debug
                    OptimizeContext.CI = wsConfig.SourceControl.CI
                    OptimizeContext.BranchOrTag = wsConfig.SourceControl.BranchOrTag
                    OptimizeContext.ProjectPaths = projectPaths
                }
                let parameters = 
                    match context.Arguments with
                    | Value.Map map -> Value.Map (map |> Map.add "context" (Value.Object optContext))
                    | _ -> failwith "internal error"

                let optCommand = $"bulk_{context.Command}"
                let result = Extensions.invokeScriptMethod<Action list> optCommand parameters (Some context.Script)
                match result with
                | Extensions.InvocationResult.Success actionBatch -> Some actionBatch
                | _ -> None
            )

        let optimizedActions =
            target.Actions
            |> List.choose optimizeAction

        // did we optimize everything ?
        if optimizedActions.Length = target.Actions.Length then
            optimizedClusters <- optimizedClusters |> Map.add cluster optimizedActions

    printfn $"clusters = {clusters.Count}"
    for (KeyValue(cluster, nodes)) in clusterNodes do
        printfn $"{cluster} => {nodes.Count}"

    printfn $"Optimized clusters = {optimizedClusters.Count}"
    for (KeyValue(cluster, actions)) in optimizedClusters do
        printfn $"{cluster} => {actions}"
