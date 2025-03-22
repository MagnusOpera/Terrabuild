namespace Terrabuild.Configuration.AST.HCL
open Terrabuild.Expressions


type [<RequireQualifiedAccess>] Attribute =
    { Name: string
      Value: Expr }
with
    static member Build name value =
        { Attribute.Name = name
          Attribute.Value = value }

    static member Append (attributes: Attribute list) (attribute: Attribute) =
        if attributes |> List.exists (fun a -> a.Name = attribute.Name) then
            Errors.raiseParseError $"Duplicate attribute: {attribute.Name}"
        else
            attributes @ [attribute]


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
