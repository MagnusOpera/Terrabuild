module Transpiler.Common
open Terrabuild.HCL.AST
open AST.Common
open Helpers

let toExtension (block: Block) =
    block
    |> checkNoId
    |> checkAllowedAttributes ["container"; "platform"; "variables"; "script"; "defaults"]
    |> checkAllowedNestedBlocks ["defaults"]
    |> ignore

    let container = block |> tryFindAttribute "container"
    let platform = block |> tryFindAttribute "platform"
    let variables = block |> tryFindAttribute "variables"
    let script = block |> tryFindAttribute "script"
    let defaults =
        block
        |> tryFindBlock "defaults"
        |> Option.map (fun defaults ->
            defaults
            |> checkNoNestedBlocks
            |> ignore

            defaults.Attributes
            |> List.map (fun a -> (a.Name, a.Value))
            |> Map.ofList)

    { ExtensionBlock.Container = container
      ExtensionBlock.Platform = platform
      ExtensionBlock.Variables = variables
      ExtensionBlock.Script = script
      ExtensionBlock.Defaults = defaults } 

let toLocals (block: Block) =
    block
    |> checkNoNestedBlocks
    |> ignore

    let variables = block.Attributes
                    |> List.map (fun a -> (a.Name, a.Value))
                    |> Map.ofList
    variables


