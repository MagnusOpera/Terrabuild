module GraphDef
open Collections


[<RequireQualifiedAccess>]
type Node = {
    Id: string
    Label: string

    Project: string
    Target: string
    TargetOperation: Configuration.TargetOperation option
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
    IsForced: bool

    // tell if outputs of a node are required or not
    // if outputs are required they can be downloaded from the cache if they exists (ProjectHash/Target/TargetHash)
    // set by Analysis/Builder (init from user) & Analysis/Requirements
    IsRequired: bool

    // tell this task is the final in the operation execution chain
    // set by Transform/TaskBuilder
    IsLast: bool
}


[<RequireQualifiedAccess>]
type Graph = {
    Nodes: Map<string, Node>
    RootNodes: string set
}






let render (graph: Graph) =
    let clusterColors =
        graph.Nodes
        |> Seq.map (fun (KeyValue(nodeId, node)) ->
            let hash = Hash.sha256 node.TargetHash
            node.TargetHash, $"#{hash.Substring(0, 3)}")
        |> Map.ofSeq

    let clusters =
        graph.Nodes
        |> Seq.groupBy (fun (KeyValue(_, node)) -> node.TargetHash)
        |> Map.ofSeq
        |> Map.map (fun _ v -> v |> Seq.map (fun kvp -> kvp.Value) |> List.ofSeq)

    let mermaid = [
        "flowchart LR"
        $"classDef forced stroke:red,stroke-width:3px"
        $"classDef required stroke:orange,stroke-width:3px"
        $"classDef selected stroke:black,stroke-width:3px"

        for (KeyValue(cluster, nodes)) in clusters do
            let clusterNode = nodes |> List.tryFind (fun node -> node.Id = cluster)
            let isCluster = clusterNode |> Option.isSome

            if isCluster then $"subgraph {cluster}[batch {clusterNode.Value.Target}]"

            let offset, nodes =
                if isCluster then "  ", nodes |> List.filter (fun node -> node.Id <> cluster)
                else "", nodes

            for node in nodes do
                $"{offset}{node.Id}([{node.Label}])"

            if isCluster then
                "end"
                $"classDef cluster-{cluster} stroke:{clusterColors[cluster]},stroke-width:3px,fill:white,rx:10,ry:10"
                $"class {cluster} cluster-{cluster}"

            for srcNode in nodes do
                for dependency in srcNode.Dependencies do
                    if dependency <> cluster then
                        let dstNode = graph.Nodes |> Map.find dependency
                        $"{srcNode.Id} --> {dstNode.Id}"

                if srcNode.IsForced then $"class {srcNode.Id} forced"
                elif srcNode.IsRequired then $"class {srcNode.Id} required"
                elif graph.RootNodes |> Set.contains srcNode.Id then $"class {srcNode.Id} selected"
    ]

    mermaid

