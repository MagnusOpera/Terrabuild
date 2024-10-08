module Mermaid
open GraphDef



type GetNodeStatus = string -> string

type GetBuildAction = string -> Build.BuildAction

let render (getNodeStatus: GetNodeStatus option) (getBuildAction: GetBuildAction option) (graph: Graph) =
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

            let buildAction =
                getBuildAction
                |> Option.map (fun getBuildAction -> getBuildAction node.Id)
                |> Option.defaultValue Build.BuildAction.Unknown

            match buildAction with
            | Build.BuildAction.Build -> $"class {node.Id} build"
            | Build.BuildAction.Ignore -> $"class {node.Id} restore"
            | _ -> $"class {node.Id} ignore"
    ]

    mermaid

