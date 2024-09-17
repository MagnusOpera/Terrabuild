module GraphDef
open Collections

[<RequireQualifiedAccess>]
type ContaineredShellOperation = {
    Container: string option
    ContainerVariables: string set
    MetaCommand: string
    Command: string
    Arguments: string
}

[<RequireQualifiedAccess>]
type NodeUsage =
    | Selected
    | Used
    | Build of Configuration.TargetOperation
with member this.ShallBuild = match this with | Build _ -> true | _ -> false 


[<RequireQualifiedAccess>]
type Node = {
    Id: string
    Label: string

    Project: string
    Target: string
    ConfigurationTarget: Configuration.Target

    Dependencies: string set
    Outputs: string set

    ProjectHash: string
    TargetHash: string
    Operations: ContaineredShellOperation list

    // tell role of node
    Usage: NodeUsage

    // tell if a node is leaf (that is no dependencies in same project)
    IsLeaf: bool
}


[<RequireQualifiedAccess>]
type Graph = {
    Nodes: Map<string, Node>
    RootNodes: string set
}



type GetNodeStatus = string -> string

let buildCacheKey (node: Node) = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"

let render (getNodeStatus: GetNodeStatus option) (graph: Graph) =
    let mermaid = [
        "flowchart TD"
        $"classDef forced stroke:red,stroke-width:3px"
        $"classDef used stroke:orange,stroke-width:3px"
        $"classDef selected stroke:black,stroke-width:3px"

        for (KeyValue(_, node)) in graph.Nodes do
            let status =
                match getNodeStatus with
                | Some getNodeStatus -> $"{getNodeStatus node.Id} "
                | _ -> ""

            let label =
                match node.Usage with
                | NodeUsage.Build targetOperation -> $"{node.Label}\n{targetOperation.Extension} {targetOperation.Command}"
                | _ -> node.Label
            $"{node.Id}(\"{status}{label}\")"

        for (KeyValue(_, node)) in graph.Nodes do
            for dependency in node.Dependencies do
                let dstNode = graph.Nodes |> Map.find dependency
                $"{node.Id} --> {dstNode.Id}"

            match node.Usage with
            | NodeUsage.Build _ -> $"class {node.Id} forced"
            | NodeUsage.Used -> $"class {node.Id} used"
            | _ -> $"class {node.Id} selected"
    ]

    mermaid

