namespace Terrabuild.Configuration.AST.HCL
open Terrabuild.Configuration.AST
open Terrabuild.Expressions
open Errors




[<RequireQualifiedAccess>]
type BlockComponents =
    | Attribute of Attribute
    | Block of Block

and [<RequireQualifiedAccess>] Attribute =
    { Name: string
      Value: Expr }
with
    static member Build name value =
        { Attribute.Name = name
          Attribute.Value = value }

and [<RequireQualifiedAccess>] Block =
    { Resource: string
      Name: string option
      Attributes: Attribute list
      Blocks: Block list }
with
    static member Build resource name (components: BlockComponents list) =
        let attributes =
            components
            |> List.choose (function
                | BlockComponents.Attribute attr -> Some attr
                | _ -> None)

        let blocks =
            components
            |> List.choose (function
                | BlockComponents.Block block -> Some block
                | _ -> None)

        { Block.Resource = resource
          Block.Name = name
          Block.Attributes = attributes
          Block.Blocks = blocks }


type File =
    { Blocks: Block list }
with
    static member Build blocks =
        { File.Blocks = blocks }
