module GraphTransformBuilder
open Collections
open GraphDef
open System.Collections.Concurrent

let build (graph: GraphDef.Graph) =
    let allNodes = ConcurrentDictionary<string, GraphDef.Node>()
    for (KeyValue(_, node)) in graph.Nodes do

        if node.IsForced then
            let nbOps = node.ConfigurationTarget.Operations.Length
            node.ConfigurationTarget.Operations
            |> List.fold (fun (dependencies, index) operation ->
                let isLast = nbOps = index

                // generate a node for each operation
                let actionNode =
                    { node with
                        Label = $"{node.Id} {operation.Extension} {operation.Command}"
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
