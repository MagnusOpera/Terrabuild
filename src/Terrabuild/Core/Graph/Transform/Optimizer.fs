module GraphTransformOptimizer
open GraphDef
open System
open System.Collections
open Collections
open Terrabuild.PubSub
open Serilog
open Errors

let optimize (sourceControl: Contracts.SourceControl) (graph: Graph) (options: Configuration.Options) =
    let startedAt = DateTime.UtcNow

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
                let childrenClusters =
                    awaitedDependencies
                    |> Seq.map (fun node -> node.Value.OperationHash)
                    |> Set.ofSeq

                let declare, clusterId =
                    if node.IsForced |> not then
                        // node is not built => let it standalone in own cluster
                        Log.Debug("Node {node} does not need rebuild", node.Id)
                        true, node.Id
                    elif nodeDependencies = Set.empty then
                        // node has no dependencies hence can freely create or join a cluster
                        Log.Debug("Node {node} has no dependencies and joined {cluster}", node.Id, node.OperationHash)
                        true, node.OperationHash
                    elif childrenClusters = Set.singleton node.OperationHash then
                        // children's cluster is same as this node so join the cluster
                        Log.Debug("Node {node} is compatible with {cluster}", node.Id, node.OperationHash)
                        true, node.OperationHash
                    else
                        // different clusters for children - maybe another time
                        Log.Debug("Node {node} can't join any clusters", node.Id)
                        false, node.Id

                if declare then
                    lock clusters (fun () -> clusters.AddOrUpdate(clusterId, add, update) |> ignore)
                declare

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
    let allNodes = Concurrent.ConcurrentDictionary<string, GraphDef.Node>()
    for (KeyValue(_, nodeIds)) in clusterNodes do
        let oneNodeId = nodeIds |> Seq.head
        let oneNode = graph.Nodes |> Map.find oneNodeId

        let nodes =
            nodeIds
            |> Seq.map (fun nodeId -> graph.Nodes |> Map.find nodeId)
            |> List.ofSeq

        let projectPaths = nodes |> List.map (fun node -> node.Id, node.Project) |> Map.ofList

        // cluster dependencies gather all nodeIds dependencies
        // nodes forming the cluster are removed (no-self dependencies)
        let clusterDependencies =
            nodes
            |> Seq.collect (fun node -> node.Dependencies) |> Set.ofSeq
        let clusterDependencies = clusterDependencies - nodeIds

        match oneNode.TargetOperation with
        | Some targetOperation ->
            let optContext = {
                Terrabuild.Extensibility.ActionContext.Debug = options.Debug
                Terrabuild.Extensibility.ActionContext.CI = sourceControl.CI
                Terrabuild.Extensibility.ActionContext.Command = targetOperation.Command
                Terrabuild.Extensibility.ActionContext.BranchOrTag = sourceControl.BranchOrTag
                Terrabuild.Extensibility.ActionContext.TempDir = ".terrabuild"
                Terrabuild.Extensibility.ActionContext.Projects = projectPaths
            }

            let parameters = 
                match targetOperation.Context with
                | Terrabuild.Expressions.Value.Map map ->
                    map
                    |> Map.add "context" (Terrabuild.Expressions.Value.Object optContext)
                    |> Terrabuild.Expressions.Value.Map
                | _ -> TerrabuildException.Raise("Failed to get context (internal error)")

            let executionRequest =
                match Extensions.invokeScriptMethod<Terrabuild.Extensibility.ActionExecutionRequest> optContext.Command parameters (Some targetOperation.Script) with
                | Extensions.InvocationResult.Success executionRequest -> executionRequest
                | _ -> TerrabuildException.Raise("Failed to get shell operation (extension error)")

            let clusterHash =
                clusterDependencies
                |> Seq.map (fun nodeId -> graph.Nodes[nodeId].Id)
                |> Hash.sha256strings
            let cluster = $"cluster-{oneNode.Id}" |> Hash.sha256

            // create the batch node if required
            let clusterDependencies = 
                match executionRequest.PreOperations with
                | [] -> clusterDependencies
                | _ ->
                    let clusterNode: GraphDef.Node = {
                        oneNode with
                            Id = cluster
                            Label = $"batch-{oneNode.Target} {cluster}"
                            Project = $"batch/{cluster}"
                            Dependencies = clusterDependencies
                            Outputs = Set.empty
                            ProjectHash = clusterHash
                            OperationHash = cluster
                            ShellOperations = executionRequest.PreOperations }
                    allNodes.TryAdd(clusterNode.Id, clusterNode) |> ignore
                    Set.singleton clusterNode.Id

            // patch each nodes to have a dependency on the cluster
            // but still keep dependencies because we want to ensure build order
            for node in nodes do
                let ops =
                    match executionRequest.Operations with
                    | Terrabuild.Extensibility.All ops -> ops
                    | Terrabuild.Extensibility.Each map -> map[node.Id]

                let node =
                    { node with
                        OperationHash = cluster
                        Dependencies = node.Dependencies + clusterDependencies
                        ShellOperations = ops }

                allNodes.TryAdd(node.Id, node) |> ignore
        | _ ->
            for node in nodes do
                allNodes.TryAdd(node.Id, node) |> ignore

    let endedAt = DateTime.UtcNow

    let optimizationDuration = endedAt - startedAt
    Log.Debug("Optimization: {duration}", optimizationDuration)

    { graph with
        Nodes = allNodes |> Map.ofDict }
