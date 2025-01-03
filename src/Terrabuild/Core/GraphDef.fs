module GraphDef
open Collections
open Terrabuild.Extensibility

[<RequireQualifiedAccess>]
type ContaineredShellOperation =
    { Container: string option
      ContainerVariables: string set
      MetaCommand: string
      Command: string
      Arguments: string }

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

      Cache: Terrabuild.Extensibility.Cacheability
      Fingerprints: ContaineredShellOperation list
      Operations: ContaineredShellOperation list
  
      // tell if a node is leaf (that is no dependencies in same project)
      IsLeaf: bool }


[<RequireQualifiedAccess>]
type Graph =
    { Nodes: Map<string, Node>
      RootNodes: string set }


let buildCacheKey (node: Node) = $"{node.ProjectHash}/{node.Target}/{node.TargetHash}"
