module GraphTransformer
open Collections
open GraphDef
open System.Collections.Concurrent
open Serilog

let transform (graph: GraphDef.Graph) =
    Log.Debug("===== [Graph Transform] =====")

    let allNodes = ConcurrentDictionary<string, GraphDef.Node>()
    for (KeyValue(_, node)) in graph.Nodes do

        if node.TargetOperation.IsSome then
            let nbOps = node.ConfigurationTarget.Operations.Length
            node.ConfigurationTarget.Operations
            |> List.fold (fun (dependencies, index) operation ->
                let isLast = nbOps = index

                // generate a node for each operation
                let actionNode =
                    { node with
                        TargetOperation = Some operation
                        OperationHash = operation.Hash
                        Dependencies = dependencies
                        IsFirst = index = 1
                        IsLast = isLast }

                let actionNode =
                    if isLast then actionNode
                    else { actionNode with Id = $"{node.Id}-{index}" }
                allNodes.TryAdd(actionNode.Id, actionNode) |> ignore

                (actionNode.Id |> Set.singleton, index+1)
            ) (node.Dependencies, 1)
            |> ignore
        else
            allNodes.TryAdd(node.Id, node) |> ignore

    { graph with 
        Graph.Nodes = allNodes |> Map.ofDict }
