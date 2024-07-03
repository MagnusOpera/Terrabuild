module GraphAnalysisRequirements
open System
open Collections
open System.Collections.Concurrent
open Terrabuild.PubSub
open Errors
open Serilog


// here we are trying to determine if outputs of a node will really be consumed (IsRequired)
// idea is if a parent node does not build (hence not required) then it's not worth downloading outputs of children
// graph is explored from top to bottom to propagate required state to children
let markRequired (graph: GraphDef.Graph) (options: Configuration.Options) =
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

    let allNodes = ConcurrentDictionary<string, GraphDef.Node>()
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
                awaitedDependencies
                |> Seq.fold (fun parentRequired dep -> parentRequired || dep.Value) node.IsRequired

            let requiredNode = { node with GraphDef.Node.IsRequired = childRequired }
            allNodes.TryAdd(depNodeId, requiredNode) |> ignore
            nodeComputed.Value <- childRequired)

    let status = hubOutputs.WaitCompletion()
    match status with
    | Status.Ok -> ()
    | Status.SubcriptionNotRaised projectId -> TerrabuildException.Raise($"Node {projectId} is unknown")
    | Status.SubscriptionError exn -> TerrabuildException.Raise("Optimization error", exn)

    let endedAt = DateTime.UtcNow
    let requiredDuration = endedAt - startedAt
    Log.Debug("Graph Requirements: {duration}", requiredDuration)

    { graph with GraphDef.Graph.Nodes = allNodes |> Map.ofDict }
