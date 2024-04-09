module Terrabuild.Expressions.Eval
open Terrabuild.Expressions

let rec eval (versions: Map<string, string>) (variables: Map<string, string>) (expr: Expr) =
    let rec eval (expr: Expr) =
        match expr with
        | Expr.Nothing -> Value.Nothing
        | Expr.Boolean bool -> Value.Bool bool
        | Expr.String str -> Value.String str
        | Expr.Number num -> Value.Number num
        | Expr.Object obj -> Value.Object obj
        | Expr.Variable var ->
            match variables |> Map.tryFind var with
            | None -> failwith $"Variable '{var}' is not defined"
            | Some str -> Value.String str
        | Expr.Map map -> map |> Map.map (fun _ expr -> eval expr) |> Value.Map
        | Expr.Function (f, exprs) ->
            let values = exprs |> List.map eval
            match f, values with
            | Function.Plus, [Value.String left; Value.String right] -> Value.String (left + right)
            | Function.Plus, [Value.String left; Value.Nothing] -> Value.String (left)
            | Function.Plus, [Value.Nothing; Value.String right] -> Value.String (right)

            | Function.Plus, [Value.Number left; Value.Number right] -> Value.Number (left + right)
            | Function.Plus, [Value.Number left; Value.Nothing] -> Value.Number (left)
            | Function.Plus, [Value.Nothing; Value.Number right] -> Value.Number (right)

            | Function.Trim, [Value.String str] -> Value.String (str.Trim())
            | Function.Trim, [Value.Nothing] -> Value.Nothing

            | Function.Upper, [Value.String str] -> Value.String (str.ToUpperInvariant())
            | Function.Upper, [Value.Nothing] -> Value.Nothing

            | Function.Lower, [Value.String str] -> Value.String (str.ToLowerInvariant())
            | Function.Lower, [Value.Nothing] -> Value.Nothing

            | Function.Version, [Value.String str] -> 
                match versions |> Map.tryFind str with
                | Some version -> Value.String version
                | _ -> failwith $"Invalid project reference {str}"

            | _ -> failwith $"Invalid arguments for function {f}"

    try
        eval expr
    with
    | exn ->
        failwith $"{exn.Message} while evaluating:\n{expr}\nand variables:\n{variables}"
