module Mermaid
open GraphDef



type GetNodeStatus = string -> string

type GetNodeOrigin = string -> Cache.Origin option

let render (getNodeStatus: GetNodeStatus option) (getOrigin: GetNodeOrigin option) (graph: Graph) =
    let mermaid = [
        "flowchart TD"
        $"classDef build stroke:red,stroke-width:3px"
        $"classDef restore stroke:orange,stroke-width:3px"
        $"classDef ignore stroke:black,stroke-width:3px"

        for (KeyValue(_, node)) in graph.Nodes do
            let status =
                getNodeStatus
                |> Option.map (fun getNodeStatus -> $"\n{getNodeStatus node.Id} ")
                |> Option.defaultValue ""

            let label = node.Label
            $"{node.Id}(\"{label}{status}\")"

        for (KeyValue(_, node)) in graph.Nodes do
            for dependency in node.Dependencies do
                let dstNode = graph.Nodes |> Map.find dependency
                $"{node.Id} --> {dstNode.Id}"

            let origin =
                getOrigin
                |> Option.bind (fun getOrigin -> getOrigin node.Id)

            match origin with
            | Some Cache.Origin.Local -> $"class {node.Id} build"
            | Some Cache.Origin.Remote -> $"class {node.Id} restore"
            | _ -> $"class {node.Id} ignore"
    ]

    mermaid

