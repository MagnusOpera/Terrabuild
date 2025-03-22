namespace Terrabuild.Configuration.AST.HCL
open Terrabuild.Configuration.AST
open Terrabuild.Expressions
open Errors




type [<RequireQualifiedAccess>] Attribute =
    { Name: string
      Value: Expr }
with
    static member Build name value =
        { Attribute.Name = name
          Attribute.Value = value }

type [<RequireQualifiedAccess>] Block =
    { Resource: string
      Name: string option
      Attributes: Attribute list
      Blocks: Block list }
with
    static member Build resource name (attributes, blocks) =
        { Block.Resource = resource
          Block.Name = name
          Block.Attributes = attributes
          Block.Blocks = blocks }


type File =
    { Blocks: Block list }
with
    static member Build blocks =
        { File.Blocks = blocks }
