module Optimizer
open GraphDef
open System
open Terrabuild.Extensibility
open System.Collections
open Collections
open Terrabuild.PubSub
open Serilog
open Errors

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
                        if nodeDependencies = Set.empty || clusters.ContainsKey(node.Cluster) then
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

    let mutable graph = graph
    for (KeyValue(cluster, nodeIds)) in clusterNodes do
        let oneNodeId = nodeIds |> Seq.head
        let oneNode = graph.Nodes |> Map.find oneNodeId

        let nodes =
            nodeIds
            |> Seq.map (fun nodeId -> graph.Nodes |> Map.find nodeId)
            |> List.ofSeq

        let projects =  nodes |> List.map (fun node -> node.Id, node.Project) |> Map.ofList



        let generateCluster dependencies (targetOperation: Configuration.TargetOperation) =
            let context =
                { ActionContext.Debug = options.Debug
                  ActionContext.CI = configuration.SourceControl.CI
                  ActionContext.Command = targetOperation.Command
                  ActionContext.BranchOrTag = configuration.SourceControl.BranchOrTag
                  ActionContext.TempDir = ".terrabuild"
                  ActionContext.Projects = projects }

            let parameters = 
                match targetOperation.Context with
                | Terrabuild.Expressions.Value.Map map ->
                    map
                    |> Map.add "context" (Terrabuild.Expressions.Value.Object context)
                    |> Terrabuild.Expressions.Value.Map
                | _ -> TerrabuildException.Raise("internal error")

            let execRequest =
                match Extensions.invokeScriptMethod<ActionExecutionRequest> targetOperation.Command parameters (Some targetOperation.Script) with
                | Extensions.InvocationResult.Success execRequest -> execRequest
                | _ -> TerrabuildException.Raise("Failed to create shell operations")

            let cluster = $"cluster-{oneNode.Id}-{targetOperation.Extension}" |> Hash.sha256
            let dependencies =
                match execRequest.PreOperations with
                | [] -> dependencies
                | _ ->
                    let clusterNode = {
                        GraphDef.Node.Id = cluster
                        Hash = cluster
                        Project = $"batch/{cluster}"
                        Target = oneNode.Target
                        Label = $"batch-{oneNode.Target} {cluster}"
                        Dependencies = dependencies
                        ProjectHash = cluster
                        Outputs = Set.empty
                        IsLeaf = oneNode.IsLeaf
                        Forced = true
                        Required = true
                        Cluster = cluster

                        Cache = Cacheability.Never
                        ShellOperations = execRequest.PreOperations
                        ConfigOperations = [ targetOperation ]
                        BuildSummary = None }
                    graph <- { graph with Nodes = graph.Nodes |> Map.add clusterNode.Id clusterNode }
                    Set [ clusterNode.Id ]

            let dependencies =
                match execRequest.Operations with
                | All operations ->
                    match operations with
                    | [] -> dependencies
                    | operations ->
                        let tempNodes =
                            nodes
                            |> List.map (fun node ->
                                let nodeId = $"node-{node.Id}-{targetOperation.Extension}" |> Hash.sha256
                                let node = {
                                    Id = nodeId
                                    Hash = nodeId
                                    Project = $"batch/{nodeId}"
                                    Target = oneNode.Target
                                    Label = $"batch-{oneNode.Target} {nodeId}"
                                    Dependencies = dependencies
                                    ProjectHash = cluster
                                    Outputs = Set.empty
                                    IsLeaf = oneNode.IsLeaf
                                    Forced = true
                                    Required = true
                                    Cluster = cluster

                                    Cache = Cacheability.Never
                                    ShellOperations = operations
                                    ConfigOperations = [ targetOperation ]
                                    BuildSummary = None }
                                graph <- { graph with Nodes = graph.Nodes |> Map.add node.Id node }
                                node)
                        tempNodes
                        |> List.map (fun node -> node.Id)
                        |> Set.ofList
                | Each operations -> Set.empty

            ()

        // cluster dependencies gather all nodeIds dependencies
        // nodes forming the cluster are removed (no-self dependencies)
        let clusterDependencies =
            nodes
            |> Seq.collect (fun node -> node.Dependencies) |> Set.ofSeq
        let clusterDependencies = clusterDependencies - nodeIds

        oneNode.ConfigOperations |> List.iteri (fun opeIndex operation -> generateCluster clusterDependencies operation)
        



    //     let optimizedActions =
    //         target.Operations
    //         |> List.choose optimizeAction

    //     // did we optimize everything ?
    //     if optimizedActions.Length = target.Actions.Length then
    //         let cluster = $"cluster-{oneNode.Id}" |> Hash.sha256
    //         let clusterNode = {
    //             Node.Id = cluster
    //             Node.Hash = clusterHash
    //             Node.Project = $"batch/{cluster}"
    //             Node.Target = oneNode.Target
    //             Node.Label = $"batch-{oneNode.Target} {cluster}"
    //             Node.Dependencies = clusterDependencies
    //             ProjectHash = clusterHash
    //             Outputs = Set.empty
    //             Cache = oneNode.Cache
    //             IsLeaf = oneNode.IsLeaf
    //             Batched = false
    //             Required = true
    //             Forced = true
    //             BuildSummary = None

    //             Cluster = cluster
    //             CommandLines = optimizedActions
    //         }
    //                 Some clusterNode
    //             else
    //                 Log.Debug("Failed to optimize cluster {cluster}", cluster)
    //                 None
    //         else
    //             Log.Debug("Cluster {cluster} has only 1 node", cluster)
    //             None

    //     // match clusterNode with
    //     // | Some clusterNode -> graph <- { graph with Nodes = graph.Nodes |> Map.add clusterNode.Id clusterNode }
    //     // | _ -> ()
            
    //     // // patch each nodes to have a dependency on the cluster
    //     // // but still keep dependencies because outputs must be recovered
    //     // for node in nodes do
    //     //     let node =
    //     //         match clusterNode with
    //     //         | Some clusterNode ->
    //     //             { node with
    //     //                 Dependencies = node.Dependencies |> Set.add clusterNode.Id
    //     //                 Batched = true
    //     //                 CommandLines = List.Empty
    //     //                 Cluster = clusterNode.Id }
    //     //         | _ ->
    //     //             { node with Cluster = $"cluster-{node.Id}" |> Hash.sha256 }

    //     //     graph <- { graph with
    //     //                     Nodes = graph.Nodes |> Map.add node.Id node }

    // let endedAt = DateTime.UtcNow

    // let optimizationDuration = endedAt - startedAt
    // Log.Debug("Optimization: {duration}", optimizationDuration)
    // graph
