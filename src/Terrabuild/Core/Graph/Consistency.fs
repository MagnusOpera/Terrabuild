module GraphConsistency
open System
open Collections
open Serilog


let enforce (options: Configuration.Options) (cache: Cache.ICache) (graph: GraphDef.Graph) =
    let startedAt = DateTime.UtcNow
    let allowRemoteCache = options.LocalOnly |> not

    let mutable nodes = graph.Nodes

    let rec enforce (parentStartTime: DateTime) (parentRequired: bool) (node: GraphDef.Node) =
        let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"

        let startTime, node =
            match cache.TryGetSummary allowRemoteCache cacheEntryId with
            | Some summary ->
                Log.Debug("{nodeId} has existing build summary", node.Id)
                if summary.Status = Cache.TaskStatus.Failure && options.Retry then
                    Log.Debug("{nodeId} must rebuild because node is failed and retry requested", node.Id)
                    DateTime.MaxValue, { node with IsForced = true; IsRequired = true }
                elif parentStartTime < summary.StartedAt then
                    Log.Debug("{nodeId} must rebuild because it is younger than parent", node.Id)
                    DateTime.MaxValue, { node with IsForced = true; IsRequired = true }
                else
                    summary.EndedAt, { node with IsRequired = node.IsRequired || parentRequired }
            | _ ->
                Log.Debug("{nodeId} has no build summary", node.Id)
                DateTime.MaxValue, { node with IsForced = true; IsRequired = true }

        let isReferenced = node.IsForced || node.IsRequired
        if isReferenced then
            nodes <- nodes |> Map.add node.Id node
            node.Dependencies |> Set.iter (fun nodeId ->
                let node = nodes |> Map.find nodeId
                enforce startTime isReferenced node |> ignore)
        isReferenced

    let rootNodes = graph.RootNodes |> Set.filter (fun nodeId ->
        let node = nodes |> Map.find nodeId
        let node = { node with IsForced = options.Force; IsRequired = options.Force || node.IsRequired }
        enforce DateTime.MaxValue options.Force node)

    let endedAt = DateTime.UtcNow
    let trimDuration = endedAt - startedAt
    Log.Debug("Graph Consistency: {duration}", trimDuration)

    { GraphDef.Graph.RootNodes = rootNodes
      GraphDef.Graph.Nodes = nodes }
