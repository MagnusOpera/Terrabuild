module Terrabuild.Expressions.Eval
open Terrabuild.Expressions

let rec eval (variables: Map<string, string>) (expr: Expr) =
    let rec eval (expr: Expr) =
        match expr with
        | Expr.Nothing -> Value.Nothing
        | Expr.Boolean bool -> Value.Bool bool
        | Expr.String str -> Value.String str
        | Expr.Variable var ->
            match variables |> Map.tryFind var with
            | None -> failwith $"Variable '{var}' is not defined"
            | Some str -> Value.String str
        | Expr.Map map -> map |> Map.map (fun _ expr -> eval expr) |> Value.Map
        | Expr.Function (f, exprs) ->
            let values = exprs |> List.map eval
            match f, values with
            | Function.Plus, [Value.String left; Value.String right] -> Value.String (left + right)
            | Function.Trim, [Value.String str] -> Value.String (str.Trim())
            | Function.Upper, [Value.String str] -> Value.String (str.ToUpperInvariant())
            | Function.Lower, [Value.String str] -> Value.String (str.ToLowerInvariant())
            | _ -> failwith $"Invalid arguments for function {f}"

    eval expr
