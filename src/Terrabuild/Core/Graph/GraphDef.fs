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

    // tell if a node is leaf (that is no dependencies in same project)
    IsLeaf: bool

    // tell if a node must be rebuild (requested by user)
    // if forced then cache is ignored
    // set by Analysis/Builder (init from user) & Analysys/Consistency
    TargetOperation: Configuration.TargetOperation option
    Operations: ContaineredShellOperation list

    // tell if outputs of a node are required or not
    // if outputs are required they can be downloaded from the cache if they exists (ProjectHash/Target/TargetHash)
    // set by Analysis/Builder (init from user) & Analysis/Requirements
    IsRequired: bool

    // tell this task is the first in the operation execution chain
    IsFirst: bool

    // tell this task is the final in the operation execution chain
    // set by Transform/TaskBuilder
    IsLast: bool

    // tell if a node is batched or not
    IsBatched: bool
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
        $"classDef required stroke:orange,stroke-width:3px"
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
                    match node.TargetOperation with
                    | None -> node.Label
                    | Some targetOperation -> $"{node.Id}\n{targetOperation.Extension} {targetOperation.Command}"
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

                if srcNode.TargetOperation.IsSome then $"class {srcNode.Id} forced"
                elif srcNode.IsRequired then $"class {srcNode.Id} required"
                elif graph.RootNodes |> Set.contains srcNode.Id then $"class {srcNode.Id} selected"
    ]

    mermaid

