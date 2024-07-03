module GraphTransformBuilder
open Collections
open GraphDef
open System.Collections.Concurrent

let build (graph: GraphDef.Graph) =
    let allNodes = ConcurrentDictionary<string, GraphDef.Node>()
    for (KeyValue(_, node)) in graph.Nodes do

        let nbOps = node.ConfigurationTarget.Operations.Length
        let nodeDependencies, _ =
            node.ConfigurationTarget.Operations
            |> List.fold (fun (dependencies, index) operation -> 
                let actionNode =
                    { node with
                        Node.Id = $"{node.Id}-{index}"
                        Node.Label = $"{node.Id} {index}/{nbOps}"
                        Node.TargetOperation = Some operation
                        Node.OperationHash = operation.Hash
                        Node.Dependencies = dependencies
                        Node.IsLast = false }
                allNodes.TryAdd(actionNode.Id, actionNode) |> ignore
                (actionNode.Id |> Set.singleton, index+1)
            ) (node.Dependencies, 1)

        let node = { node with Node.Dependencies = nodeDependencies }
        allNodes.TryAdd(node.Id, node) |> ignore

    { graph with 
        Graph.Nodes = allNodes |> Map.ofDict }
