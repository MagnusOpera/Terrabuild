module Terrabuild.Expressions.Dependencies
open Terrabuild.Expressions


let rec find (expr: Expr) =
    let rec eval (varUsed: Set<string>) (expr: Expr) =
        match expr with
        | Expr.Variable var -> varUsed |> Set.add var
        | Expr.Map map -> map |> Map.fold (fun varUsed _ v -> varUsed + eval varUsed v) varUsed
        | Expr.List list -> list |> List.fold (fun varUsed v -> varUsed + eval varUsed v) varUsed
        // WARNING: here we are treating specifically the AST for . operator to reconstruct the var dependency
        | Expr.Function (Function.Item, [lexpr; rexpr]) ->
            let lvarUsed = eval Set.empty lexpr |> List.ofSeq
            match lvarUsed, rexpr with
            | [lvarUsed], Expr.String rvarUsed -> varUsed |> Set.add $"{lvarUsed}.{rvarUsed}"
            | _ -> Errors.raiseBugError $"Unexpected AST: {expr}"
        // WARNING: here we are treating specifically the AST for .? operator to reconstruct the var dependency
        | Expr.Function (Function.TryItem, [lexpr; rexpr]) ->
            let lvarUsed = eval Set.empty lexpr |> List.ofSeq
            match lvarUsed, rexpr with
            | [lvarUsed], Expr.String rvarUsed -> varUsed |> Set.add $"{lvarUsed}.{rvarUsed}"
            | _ -> Errors.raiseBugError $"Unexpected AST: {expr}"
        | Expr.Function (_, exprs) -> exprs |> List.fold (fun varUsed expr -> varUsed + eval varUsed expr) varUsed
        | _ -> varUsed

    eval Set.empty expr
