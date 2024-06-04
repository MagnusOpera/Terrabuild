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
                | Function.Plus, [Value.Number left; Value.Number right] -> Value.Number (left + right)
                | Function.Minus, [Value.Number left; Value.Number right] -> Value.Number (left - right)
                | Function.Trim, [Value.String str] -> Value.String (str.Trim())

                | Function.Upper, [Value.String str] -> Value.String (str.ToUpperInvariant())
                | Function.Upper, [Value.Nothing] -> Value.Nothing

                | Function.Lower, [Value.String str] -> Value.String (str.ToLowerInvariant())
                | Function.Lower, [Value.Nothing] -> Value.Nothing

                | Function.Version, [Value.String str] ->
                    let projectName = FS.workspaceRelative context.WorkspaceDir context.ProjectDir str
                    match context.Versions |> Map.tryFind projectName with
                    | Some version -> Value.String version
                    | _ -> TerrabuildException.Raise($"Unknown project reference '{str}'")

                | Function.Item, [Value.Map map; Value.String key] ->
                    match map |> Map.tryFind key with
                    | Some value -> value
                    | _ -> TerrabuildException.Raise($"Unknown key {key}")

                | Function.Item, [Value.List list; Value.Number index] ->
                    match list |> List.tryItem index with
                    | Some value -> value
                    | _ -> TerrabuildException.Raise($"Out of range index {index}")

                | Function.TryItem, [Value.Map map; Value.String key] ->
                    match map |> Map.tryFind key with
                    | Some value -> value
                    | _ -> Value.Nothing

                | Function.TryItem, [Value.List list; Value.Number index] ->
                    match list |> List.tryItem index with
                    | Some value -> value
                    | _ -> Value.Nothing

                | Function.Coalesce, list ->
                    match list |> List.tryFind (fun i -> i <> Value.Nothing) with
                    | Some value -> value
                    | _ -> TerrabuildException.Raise($"Failed to find value")
                
                | Function.Ternary, [Value.Bool condition; trueValue; falseValue] ->
                    if condition then trueValue
                    else falseValue

                | Function.Equal, [left; right] ->
                    Value.Bool (left = right)

                | Function.NotEqual, [left; right] ->
                    Value.Bool (left <> right)

                | f, prms -> 
                    let getParamType (value: Value ) =
                        match value with
                        | Value.Bool _ -> "bool"
                        | Value.List _ -> "list"
                        | Value.Map _ -> "map"
                        | Value.Nothing -> "nothing"
                        | Value.Number _ -> "number"
                        | Value.Object _ -> "object"
                        | Value.String _ -> "string"

                    let prms = prms |> List.map (getParamType) |> String.join "*"
                    TerrabuildException.Raise($"Invalid arguments for function {f} with parameters ({prms})")
            varUsed, res

    eval Set.empty expr
