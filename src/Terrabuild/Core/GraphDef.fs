module GraphDef
open Collections

[<RequireQualifiedAccess>]
type ContaineredShellOperation = {
    Container: string option
    ContainerVariables: string set
    MetaCommand: string
    Command: string
    Arguments: string
    ExitCodes: Map<int, Terrabuild.Extensibility.StatusCode>
}

[<RequireQualifiedAccess>]
type NodeUsage =
    | Ignore
    | Restore
    | Build


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
    Cache: Terrabuild.Extensibility.Cacheability

    // tell if a node is leaf (that is no dependencies in same project)
    IsLeaf: bool

    Usage: NodeUsage
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
        $"classDef built stroke:red,stroke-width:3px"
        $"classDef used stroke:orange,stroke-width:3px"
        $"classDef ignored stroke:black,stroke-width:3px"

        for (KeyValue(_, node)) in graph.Nodes do
            let status =
                match getNodeStatus with
                | Some getNodeStatus -> $"\n{getNodeStatus node.Id} "
                | _ -> ""

            let label = node.Label
            $"{node.Id}(\"{label}{status}\")"

        for (KeyValue(_, node)) in graph.Nodes do
            for dependency in node.Dependencies do
                let dstNode = graph.Nodes |> Map.find dependency
                $"{node.Id} --> {dstNode.Id}"

            match node.Usage with
            | NodeUsage.Build -> $"class {node.Id} built"
            | NodeUsage.Restore -> $"class {node.Id} used"
            | _ -> $"class {node.Id} ignored"
    ]

    mermaid

