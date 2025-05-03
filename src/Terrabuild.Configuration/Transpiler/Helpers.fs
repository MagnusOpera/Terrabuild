module private Terrabuild.Configuration.Transpiler.Helpers
open Terrabuild.Expressions
open Errors
open Terrabuild.Lang.AST

let checkNoId (block: Block) =
    match block.Id with
    | None -> block
    | Some _ -> raiseParseError "id is not allowed"

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

let simpleEval = Eval.eval (fun _ -> failwith "unexpected") Eval.EvaluationContext.Empty
