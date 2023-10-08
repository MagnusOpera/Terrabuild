module Optimizer
open System.Collections.Generic

[<RequireQualifiedAccess>]
type BuildBatch = {
    Nodes: Graph.Node list
}

[<RequireQualifiedAccess>]
type BuildBatches = {
    Graph: Graph.WorkspaceGraph
    Batches: BuildBatch list
}

// this is Kahn's algorithm for topoligical sorting (see https://en.wikipedia.org/wiki/Topological_sorting)
let optimize (g: Graph.WorkspaceGraph) =
    // compute first incoming edges
    let incomingEdges = g.Nodes |> Map.map (fun _ _ -> ref 0)
    for KeyValue(_, node) in g.Nodes do
        for dependency in node.Dependencies do
            incomingEdges[dependency].Value <- incomingEdges[dependency].Value + 1

    // build order
    let batchedLevel = List<BuildBatch>()

    // get nodes with no incoming edges
    let rec processIncomings level noIncoming =
        if noIncoming |> Map.isEmpty |> not then
            let batchNodes = noIncoming |> Seq.map (fun (KeyValue(key, _)) -> g.Nodes[key]) |> List.ofSeq
            let batch = { BuildBatch.Nodes = batchNodes }
            batchedLevel.Add(batch)

            // decrement incoming for dependants
            noIncoming
            |> Seq.iter (fun kvp ->
                let node = g.Nodes[kvp.Key]
                incomingEdges[kvp.Key].Value <- -1
                for dependency in node.Dependencies do
                    incomingEdges[dependency].Value <- incomingEdges[dependency].Value - 1)

            let noIncoming = incomingEdges |> Map.filter (fun _ value -> value.Value = 0)
            processIncomings (level+1) noIncoming

    let noIncoming = incomingEdges |> Map.filter (fun _ value -> value.Value = 0)
    processIncomings 1 noIncoming

    let levels = batchedLevel |> List.ofSeq |> List.rev
    let batches = { BuildBatches.Graph = g
                    BuildBatches.Batches = levels }
    batches
