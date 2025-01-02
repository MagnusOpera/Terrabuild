module GraphDef
open Collections
open Terrabuild.Extensibility

[<RequireQualifiedAccess>]
type ContaineredShellOperation =
    { Container: string option
      ContainerVariables: string set
      MetaCommand: string
      FingerprintOp: ShellOperation option
      ShellOp: ShellOperation }

[<RequireQualifiedAccess>]
type Node =
    { Id: string
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
      IsLeaf: bool }


[<RequireQualifiedAccess>]
type Graph =
    { Nodes: Map<string, Node>
      RootNodes: string set }


let buildCacheKey (node: Node) = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"
