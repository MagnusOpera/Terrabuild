module GraphAnalysisConsistency
open System
open System.Collections.Concurrent
open Collections
open Terrabuild.PubSub
open Serilog
open Errors


// here we ensure graph is consistent:
// - node do not happen before child node
// - node must rebuild if child is forced
// - node must rebuild if forced
//
// ==> previous build summary is only attached if node validates rules
let enforce (graph: GraphDef.Graph) (cache: Cache.ICache) (options: Configuration.Options) =
    let startedAt = DateTime.UtcNow
    let allowRemoteCache = options.LocalOnly |> not

    let allNodes = ConcurrentDictionary<string, GraphDef.Node>()
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
                awaitedDependencies |> Seq.fold (fun (forced, lastBuild) childComputed ->
                    let childRebuild, childLastBuild = childComputed.Value
                    forced || childRebuild, max lastBuild childLastBuild) (false, DateTime.MinValue)

            let summary, nodeLastBuild =
                if node.IsForced then
                    Log.Debug("{nodeId} must rebuild because node is forced", nodeId)
                    None, DateTime.MaxValue

                elif childrenRebuild then
                    Log.Debug("{nodeId} must be rebuild because children must rebuild", nodeId)
                    None, DateTime.MaxValue

                else
                    let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"

                    // get task execution summary & take care of retrying failed tasks
                    match cache.TryGetSummary allowRemoteCache cacheEntryId with
                    | Some summary ->
                        Log.Debug("{nodeId} has existing build summary", nodeId)
                        if summary.Status = Cache.TaskStatus.Failure && options.Retry then
                            Log.Debug("{nodeId} must rebuild because node is failed and retry requested", nodeId)
                            None, DateTime.MaxValue
                        elif summary.StartedAt < childrenLastBuild then
                            Log.Debug("{nodeId} must rebuild because it is older than one of child", nodeId)
                            cache.Invalidate cacheEntryId
                            None, DateTime.MaxValue
                        else
                            Some summary, summary.EndedAt
                    | _ ->
                        Log.Debug("{nodeId} has no build summary", nodeId)
                        None, DateTime.MaxValue

            let consistentNode = { node with GraphDef.Node.IsForced = summary |> Option.isNone }
            allNodes.TryAdd(nodeId, consistentNode) |> ignore

            nodeComputed.Value <- (node.IsForced, nodeLastBuild)
        )

    let status = hub.WaitCompletion()
    match status with
    | Status.Ok -> ()
    | Status.SubcriptionNotRaised projectId -> TerrabuildException.Raise($"Node {projectId} is unknown")
    | Status.SubscriptionError exn -> TerrabuildException.Raise("Optimization error", exn)

    let endedAt = DateTime.UtcNow
    let trimDuration = endedAt - startedAt
    Log.Debug("Graph Consistency: {duration}", trimDuration)

    { graph with GraphDef.Graph.Nodes = allNodes |> Map.ofDict }
