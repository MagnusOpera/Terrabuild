module Terrabuild.Expressions.Dependencies
open System.Collections.Generic
open Terrabuild.Expressions
open FSharp.Reflection


let rec find (expr: Expr) =
    let rec eval (varUsed: Set<string>) (expr: Expr) =
        match expr with
        | Expr.Variable var -> varUsed |> Set.add var
        | Expr.Map map -> map |> Map.fold (fun varUsed _ v -> varUsed + eval varUsed v) varUsed
        | Expr.List list -> list |> List.fold (fun varUsed v -> varUsed + eval varUsed v) varUsed
        | Expr.Function (_, exprs) -> exprs |> List.fold (fun varUsed expr -> varUsed + eval varUsed expr) varUsed
        | _ -> varUsed

    eval Set.empty expr


let rec findArrayOfDependencies (expr: Expr) =

    let rec eval (varUsed: Set<string>) (expr: Expr) =
        match expr with
        | Expr.Variable var -> varUsed |> Set.add var
        | _ -> Errors.raiseInvalidArg "Array of dependencies expected"

    match expr with
    | Expr.List list -> list |> List.fold (fun varUsed v -> varUsed + eval varUsed v) Set.empty
    | _ -> Errors.raiseInvalidArg "Array of dependencies expected"


let private fsmapTy = typedefof<Map<string, _>>
let private fsoptionTy = typedefof<option<_>>
let private fskvpTy = typedefof<KeyValuePair<string, _>>
let reflectionFind (o: obj) =

    let rec reflectionFind (theObject: obj) =
        seq {
            match theObject with
            | null -> ()
            | :? System.Collections.IEnumerable as enumerable ->
                for so in enumerable do
                    yield! reflectionFind so
            | :? Expr as expr -> yield find expr
            | _ ->
                let ty = theObject.GetType()
                if ty.IsGenericType then
                    let tyDef = ty.GetGenericTypeDefinition()
                    if tyDef = fsmapTy then
                        let valuesProperty = ty.GetProperty("Values")
                        let values = valuesProperty.GetValue(theObject)
                        yield! reflectionFind values
                    elif tyDef = fsoptionTy then
                        let value = ty.GetProperty("Value").GetValue(theObject, null)
                        yield! reflectionFind value
                    elif tyDef = fskvpTy then
                        let value = ty.GetProperty("Value").GetValue(theObject, null)
                        yield! reflectionFind value
                elif FSharpType.IsRecord(ty, false) then
                    let fields = FSharpType.GetRecordFields(ty) |> Array.map (fun propInfo -> propInfo.GetValue(theObject, null))
                    for field in fields do
                        yield! reflectionFind field
        }

    o |> reflectionFind |> Seq.fold (fun acc s -> acc + s) Set.empty
