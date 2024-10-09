module Mermaid
open GraphDef



type GetStatus = Node -> string

type GetOrigin = Node -> Build.TaskRequest option

let render (getStatus: GetStatus option) (getOrigin: GetOrigin option) (graph: Graph) =
    let mermaid = [
        "flowchart TD"
        $"classDef build stroke:red,stroke-width:3px"
        $"classDef restore stroke:orange,stroke-width:3px"
        $"classDef ignore stroke:black,stroke-width:3px"

        for (KeyValue(_, node)) in graph.Nodes do
            let status =
                getStatus
                |> Option.map (fun getNodeStatus -> getNodeStatus node)
                |> Option.defaultValue ""

            $"{node.Id}(\"{node.Project}\n{node.Target} {status}\")"

        for (KeyValue(_, node)) in graph.Nodes do
            for dependency in node.Dependencies do
                let dstNode = graph.Nodes |> Map.find dependency
                $"{node.Id} --> {dstNode.Id}"

            let origin =
                getOrigin
                |> Option.bind (fun getOrigin -> getOrigin node)

            match origin with
            | Some Build.TaskRequest.Build -> $"class {node.Id} build"
            | Some Build.TaskRequest.Restore -> $"class {node.Id} restore"
            | _ -> $"class {node.Id} ignore"
    ]

    mermaid

