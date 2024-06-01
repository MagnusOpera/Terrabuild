module Terrabuild.Expressions.Eval
open Terrabuild.Expressions
open Errors

type EvaluationContext = {
    WorkspaceDir: string
    ProjectDir: string
    Versions: Map<string, string>
    Variables: Map<string, Expr>
}

let rec eval (context: EvaluationContext) (expr: Expr) =
    let rec eval (varUsed: Set<string>) (expr: Expr) =
        match expr with
        | Expr.Nothing -> varUsed, Value.Nothing
        | Expr.Boolean bool -> varUsed, Value.Bool bool
        | Expr.String str -> varUsed, Value.String str
        | Expr.Number num -> varUsed, Value.Number num
        | Expr.Object obj -> varUsed, Value.Object obj
        | Expr.Variable var ->
            match context.Variables |> Map.tryFind var with
            | None -> TerrabuildException.Raise($"Variable '{var}' is not defined")
            | Some value -> eval (varUsed |> Set.add var) value
        | Expr.Map map ->
            let varUsed, values = map |> Map.fold (fun (varUsed, map) k v ->
                let mvu, mv = eval varUsed v
                varUsed+mvu, map |> Map.add k mv) (varUsed, Map.empty)
            varUsed, Value.Map values
        | Expr.List list ->
            let varUsed, values = list |> List.fold (fun (varUsed, list) v ->
                let mvu, mv = eval varUsed v
                varUsed+mvu, list @ [mv]) (varUsed, [])
            varUsed, Value.List values
        | Expr.Function (f, exprs) ->
            let varUsed, values = exprs |> List.fold (fun (varUsed, exprs) expr ->
                let vu, e = eval varUsed expr
                varUsed+vu, (exprs @ [e])) (varUsed, [])
            let res = 
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
                    let projectName = FS.workspaceRelative context.WorkspaceDir context.ProjectDir str
                    match context.Versions |> Map.tryFind projectName with
                    | Some version -> Value.String version
                    | _ -> TerrabuildException.Raise($"Unknown project reference {str}")

                | Function.Item, [Value.Map map; Value.String key] ->
                    match map |> Map.tryFind key with
                    | Some value -> value
                    | _ -> TerrabuildException.Raise($"Unknown key {key}")

                | Function.Item, [Value.List list; Value.Number index] ->
                    match list |> List.tryItem index with
                    | Some value -> value
                    | _ -> TerrabuildException.Raise($"Out of range index {index}")

                | _ -> TerrabuildException.Raise($"Invalid arguments for function {f}")
            varUsed, res

    eval Set.empty expr
