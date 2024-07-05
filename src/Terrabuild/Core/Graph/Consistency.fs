module GraphConsistency
open System
open Collections
open Serilog


let enforce (options: Configuration.Options) (tryGetSummaryOnly: bool -> string -> Cache.TargetSummary option) (graph: GraphDef.Graph) =
    let startedAt = DateTime.UtcNow
    let allowRemoteCache = options.LocalOnly |> not

    let mutable nodes = graph.Nodes

    let rec enforce (parentStartTime: DateTime) (parentRequired: bool) (node: GraphDef.Node) =
        let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"

        let startTime, node =
            if node.IsForced then
                DateTime.MaxValue, { node with IsForced = true; IsRequired = true }
            else
                match tryGetSummaryOnly allowRemoteCache cacheEntryId with
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

        let isUsed =
            let isRequired = node.IsForced || node.IsRequired
            let childRequired =
                if options.Retry then
                    node.Dependencies
                    |> Set.map (fun nodeId ->
                        let node = nodes |> Map.find nodeId
                        enforce startTime isRequired node)
                    |> Set.exists id
                else
                    false
            isRequired || childRequired
        if isUsed then nodes <- nodes |> Map.add node.Id node
        isUsed

    let rootNodes = graph.RootNodes |> Set.filter (fun nodeId ->
        let node = nodes |> Map.find nodeId
        let node = { node with IsForced = options.Force; IsRequired = options.Force || node.IsRequired }
        enforce DateTime.MaxValue options.Force node)

    let endedAt = DateTime.UtcNow
    let trimDuration = endedAt - startedAt
    Log.Debug("Graph Consistency: {duration}", trimDuration)

    { GraphDef.Graph.RootNodes = rootNodes
      GraphDef.Graph.Nodes = nodes }
