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
    OperationHash: string
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
    let clusterColors =
        graph.Nodes
        |> Seq.map (fun (KeyValue(nodeId, node)) ->
            let hash = Hash.sha256 node.OperationHash
            node.OperationHash, $"#{hash.Substring(0, 3)}")
        |> Map.ofSeq

    let clusters =
        graph.Nodes
        |> Seq.groupBy (fun (KeyValue(_, node)) -> node.OperationHash)
        |> Map.ofSeq
        |> Map.map (fun _ v -> v |> Seq.map (fun kvp -> kvp.Value) |> List.ofSeq)

    let mermaid = [
        "flowchart TD"
        $"classDef forced stroke:red,stroke-width:3px"
        $"classDef used stroke:orange,stroke-width:3px"
        $"classDef selected stroke:black,stroke-width:3px"

        for (KeyValue(cluster, nodes)) in clusters do
            let clusterNode = nodes |> List.tryFind (fun node -> node.Id = cluster)
            let isCluster = clusterNode |> Option.isSome

            if isCluster then $"subgraph {cluster}[\" \"]"

            let offset, nodes =
                if isCluster then "  ", nodes |> List.filter (fun node -> node.Id <> cluster)
                else "", nodes

            for node in nodes do
                let status =
                    match getNodeStatus with
                    | Some getNodeStatus -> $"{getNodeStatus node.Id} "
                    | _ -> ""

                let label =
                    match node.Usage with
                    | NodeUsage.Build targetOperation -> $"{node.Label}\n{targetOperation.Extension} {targetOperation.Command}"
                    | _ -> node.Label
                $"{offset}{node.Id}(\"{status}{label}\")"

            if isCluster then
                "end"
                $"classDef cluster-{cluster} stroke:{clusterColors[cluster]},stroke-width:3px,fill:white,rx:10,ry:10"
                $"class {cluster} cluster-{cluster}"

            for srcNode in nodes do
                for dependency in srcNode.Dependencies do
                    if (isCluster && dependency = cluster) |> not then
                        let dstNode = graph.Nodes |> Map.find dependency
                        $"{srcNode.Id} --> {dstNode.Id}"

                match srcNode.Usage with
                | NodeUsage.Build _ -> $"class {srcNode.Id} forced"
                | NodeUsage.Used -> $"class {srcNode.Id} used"
                | _ -> $"class {srcNode.Id} selected"
    ]

    mermaid

