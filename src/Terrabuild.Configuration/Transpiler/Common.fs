module Transpiler.Common
open Terrabuild.Expressions
open Errors
open AST.HCL
open AST.Common


let checkNoAttributes (block: Block) =
    if block.Attributes <> [] then raiseParseError "attributes are not allowed"
    block

let checkAllowedAttributes (allowed: string list) (block: Block) =
    block.Attributes
    |> List.iter (fun a ->
        if not (allowed |> List.contains a.Name) then raiseParseError $"Unexpected attribute: {a.Name}")
    block

let checkNoNestedBlocks (block: Block) =
    if block.Blocks <> [] then raiseParseError "nested blocks are not allowed"
    block

let checkAllowedNestedBlocks (allowed: string list) (block: Block) =
    block.Blocks
    |> List.iter (fun b ->
        if not (allowed |> List.contains b.Resource) then raiseParseError $"Unexpected block: {b.Resource}")
    block

let tryFindAttribute (name: string) (block: Block) =
    block.Attributes
    |> List.tryFind (fun a -> a.Name = name)
    |> Option.map (fun a -> a.Value)

let tryFindBlock (resource: string) (block: Block) =
    let candidates = 
        block.Blocks
        |> List.choose (fun b -> if b.Resource = resource then Some b else None)
    match candidates with
    | [] -> None
    | [block] -> Some block
    | _ -> raiseParseError $"multiple {resource} declared"

let valueOrDefault (defaultValue: Expr) (attribute: Attribute option) =
    match attribute with
    | Some attribute -> attribute.Value
    | None -> defaultValue

let simpleEval = Eval.eval Eval.EvaluationContext.Empty



let toExtension (block: Block) =
    block
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


