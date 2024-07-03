module GraphDef
open Collections


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

    // tell if a node is leaf (that is no dependencies in same project)
    IsLeaf: bool

    // tell if a node must be rebuild (requested by user)
    // if forced then cache is ignored
    // set by GraphBuilder
    IsForced: bool

    // tell if outputs of a node are required or not
    // if outputs are required they can be downloaded from the cache if they exists (ProjectHash/Target/TargetHash)
    // set by GraphConsistency (bottom-up) & GraphRequirements (top-down)
    IsRequired: bool

    // tell this task is the final in the operation execution chain
    // set by TaskBuilder
    IsLast: bool
}


[<RequireQualifiedAccess>]
type Graph = {
    Nodes: Map<string, Node>
    RootNodes: string set
}

