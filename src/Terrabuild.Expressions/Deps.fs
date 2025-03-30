module Terrabuild.Expressions.Dependencies
open Terrabuild.Expressions
open Microsoft.FSharp.Reflection


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


let reflectionFind (o: obj) =
    let rec reflectionFind (o: obj) =
        seq {
            match o with
            | :? Expr as expr -> yield find expr
            | :? Option<Expr> as expr ->
                match expr with
                | Some expr -> yield find expr
                | _ -> ()
            | :? Map<string, Expr> as map -> yield! map.Values |> Seq.map find
            | :? Option<Map<string, Expr>> as map ->
                match map with
                | Some map -> yield! map.Values |> Seq.map find
                | _ -> ()
            | _ when FSharpType.IsRecord(o.GetType()) ->
                    let fields = FSharpValue.GetRecordFields(o)
                    for field in fields do yield! reflectionFind field
            | _ -> ()
        } 
        
    o |> reflectionFind |> Seq.fold (fun acc s -> acc + s) Set.empty
