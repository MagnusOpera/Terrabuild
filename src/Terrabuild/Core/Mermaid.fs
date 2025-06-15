module Mermaid
open GraphDef
open System



type GetStatus = Node -> string

type GetOrigin = Node -> Build.TaskRequest option


let scaleToPastel (value: int) : int =
    // Map from [0, 255] to [160, 240]
    160 + (value * (240 - 160) / 255)

let pastelColorFromHash (md5hash: string) : string =
    if md5hash.Length < 6 then
        invalidArg "md5hash" "MD5 hash string must be at least 6 characters long"

    let r = Convert.ToInt32(md5hash.Substring(0, 2), 16) |> scaleToPastel
    let g = Convert.ToInt32(md5hash.Substring(2, 2), 16) |> scaleToPastel
    let b = Convert.ToInt32(md5hash.Substring(4, 2), 16) |> scaleToPastel

    $"#{r:X2}{g:X2}{b:X2}"

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

            let nodeTitle =
                match node.ProjectId with
                | None -> node.Target
                | Some projectId -> $"{node.Target} {projectId}"
            $"{node.Id}(\"<b>{nodeTitle}</b> {status}\n{node.ProjectDir}\")"

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
            let color = node.ProjectDir |> Hash.md5 |> pastelColorFromHash 
            $"style {node.Id} fill:{color}"
    ]

    mermaid

