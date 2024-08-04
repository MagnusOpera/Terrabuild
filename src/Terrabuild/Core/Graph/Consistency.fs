module GraphConsistency
open System
open Collections
open Serilog


let enforce buildAt force retry (tryGetSummaryOnly: string -> Cache.TargetSummary option) (graph: GraphDef.Graph) =
    Log.Debug("===== [Graph Consistency] =====")

    let startedAt = DateTime.UtcNow

    let mutable nodes = graph.Nodes
    let processedNodes = Concurrent.ConcurrentDictionary<string, DateTime>()

    let rec markRequired nodeId =
        let node = nodes |> Map.find nodeId

        match processedNodes.TryGetValue nodeId with
        | false, _ ->
            // mark node as required since we have been invoked for this node
            let node = { node with IsRequired = true }

            // find max completion date of children
            let maxCompletionChildren =
                node.Dependencies
                |> Set.map markRequired
                |> Seq.sortDescending
                |> Seq.tryHead
                |> Option.defaultValue DateTime.MinValue

            let completionDate, node =
                // fast path: if children must rebuild do not care to check the cache
                if node.TargetOperation.IsSome then
                    Log.Debug("{nodeId} must rebuild because rebuild set on target", node.Id)
                    DateTime.MaxValue, node
                elif maxCompletionChildren = DateTime.MaxValue then
                    Log.Debug("{nodeId} must rebuild because child is rebuilding", node.Id)
                    DateTime.MaxValue, { node with TargetOperation = Configuration.TargetOperation.MarkAsForced }
                elif force then
                    Log.Debug("{nodeId} must rebuild because force build requested", node.Id)
                    DateTime.MaxValue, { node with TargetOperation = Configuration.TargetOperation.MarkAsForced }
                else
                    // slow path: check and apply consistency rules
                    let cacheEntryId = GraphDef.buildCacheKey node
                    match tryGetSummaryOnly cacheEntryId with
                    | Some summary ->
                        Log.Debug("{nodeId} has existing build summary", node.Id)
                        if summary.StartedAt < maxCompletionChildren then
                            Log.Debug("{nodeId} must rebuild because it is younger than child", node.Id)
                            DateTime.MaxValue, { node with TargetOperation = Configuration.TargetOperation.MarkAsForced }
                        elif (summary.IsSuccessful |> not) && retry then
                            Log.Debug("{nodeId} must rebuild because node is failed and retry requested", node.Id)
                            DateTime.MaxValue, { node with TargetOperation = Configuration.TargetOperation.MarkAsForced }
                        else
                            Log.Debug("{nodeId} is only marked as required", node.Id)
                            summary.EndedAt, node
                    | _ ->
                        Log.Debug("{nodeId} must be build since no summary and required", node.Id)
                        DateTime.MaxValue, { node with TargetOperation = Configuration.TargetOperation.MarkAsForced }

            nodes <- nodes |> Map.add node.Id node
            processedNodes.TryAdd(nodeId, completionDate) |> ignore
            completionDate

        | true, completionDate ->
            completionDate

    let rootNodes = graph.RootNodes |> Set.filter (fun nodeId -> buildAt < markRequired nodeId)

    let endedAt = DateTime.UtcNow
    let trimDuration = endedAt - startedAt
    Log.Debug("Graph Consistency: {duration}", trimDuration)

    { graph with
        RootNodes = rootNodes
        GraphDef.Graph.Nodes = nodes }
