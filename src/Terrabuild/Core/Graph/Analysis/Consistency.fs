module GraphAnalysisConsistency
open System
open System.Collections.Concurrent
open Collections
open Serilog


let enforce (options: Configuration.Options) (cache: Cache.ICache) (graph: GraphDef.Graph) =
    let startedAt = DateTime.UtcNow
    let allowRemoteCache = options.LocalOnly |> not

    let allNodes = ConcurrentDictionary<string, GraphDef.Node>()

    let rec enforce (parentStartTime: DateTime) (parentRequired: bool) nodeId =
        let node = graph.Nodes[nodeId]
        let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"

        let startTime, node =
            match cache.TryGetSummary allowRemoteCache cacheEntryId with
            | Some summary ->
                Log.Debug("{nodeId} has existing build summary", node.Id)
                if summary.Status = Cache.TaskStatus.Failure && options.Retry then
                    Log.Debug("{nodeId} must rebuild because node is failed and retry requested", node.Id)
                    DateTime.MaxValue, { node with IsForced = true }
                elif parentStartTime < summary.StartedAt then
                    Log.Debug("{nodeId} must rebuild because it is younger than parent", node.Id)
                    DateTime.MaxValue, { node with IsForced = true }
                else
                    summary.EndedAt, { node with IsRequired = parentRequired }
            | _ ->
                Log.Debug("{nodeId} has no build summary", node.Id)
                DateTime.MaxValue, { node with IsForced = true }

        let isReferenced = node.IsForced || node.IsRequired
        if isReferenced then
            allNodes.TryAdd(node.Id, node) |> ignore
            node.Dependencies |> Set.iter (ignore << enforce startTime isReferenced)
        isReferenced

    let rootNodes = graph.RootNodes |> Set.filter (enforce DateTime.MaxValue options.Force)

    let endedAt = DateTime.UtcNow
    let trimDuration = endedAt - startedAt
    Log.Debug("Graph Consistency: {duration}", trimDuration)

    { GraphDef.Graph.RootNodes = rootNodes
      GraphDef.Graph.Nodes = allNodes |> Map.ofDict }
