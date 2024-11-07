module Terrabuild.Expressions.Eval
open Terrabuild.Expressions
open Errors
open Collections

type EvaluationContext = {
    WorkspaceDir: string
    ProjectDir: string option
    Versions: Map<string, string>
    Variables: Map<string, Value * Set<string>>
}

let rec eval (context: EvaluationContext) (expr: Expr) =
    let rec eval (expr: Expr) =
        match expr with
        | Expr.Nothing -> Value.Nothing, Set.empty
        | Expr.Bool bool -> Value.Bool bool, Set.empty
        | Expr.String str -> Value.String str, Set.empty
        | Expr.Number num -> Value.Number num, Set.empty
        | Expr.Object obj -> Value.Object obj, Set.empty
        | Expr.Variable var ->
            // if varUsed |> Set.contains var then TerrabuildException.Raise($"Variable {var} has circular definition")
            match context.Variables |> Map.tryFind var with
            | None -> TerrabuildException.Raise($"Variable '{var}' is not defined")
            | Some (value, varUsed) -> value, (varUsed |> Set.add var)
        | Expr.Map map ->
            let values, varUsed = map |> Map.fold (fun (map, varUsed) k v ->
                let mv, mvu = eval v
                map |> Map.add k mv, varUsed+mvu) (Map.empty, Set.empty)
            Value.Map values, varUsed
        | Expr.List list ->
            let values, varUsed = list |> List.fold (fun (list, varUsed) v ->
                let mv, mvu = eval v
                list @ [mv], varUsed+mvu) ([], Set.empty)
            Value.List values, varUsed
        | Expr.Function (f, exprs) ->
            let values, varUsed = exprs |> List.fold (fun (exprs, varUsed) expr ->
                let e, vu = eval expr
                (exprs @ [e], varUsed+vu)) ([], Set.empty)
            let res =
                match f, values with
                | Function.Plus, [Value.String left; Value.String right] -> Value.String (left + right)
                | Function.Plus, [Value.Number left; Value.Number right] -> Value.Number (left + right)
                | Function.Plus, [Value.Map left; Value.Map right] -> Value.Map (left |> Map.addMap right)
                | Function.Plus, [Value.Map left; Value.Nothing] -> Value.Map left
                | Function.Plus, [Value.List left; Value.List right] -> Value.List (left @ right)
                | Function.Plus, [Value.List left; Value.Nothing] -> Value.List left
                | Function.Minus, [Value.Number left; Value.Number right] -> Value.Number (left - right)
                | Function.Trim, [Value.String str] -> Value.String (str.Trim())

                | Function.Upper, [Value.String str] -> Value.String (str.ToUpperInvariant())
                | Function.Upper, [Value.Nothing] -> Value.Nothing

                | Function.Lower, [Value.String str] -> Value.String (str.ToLowerInvariant())
                | Function.Lower, [Value.Nothing] -> Value.Nothing

                | Function.Replace, [Value.String str; Value.String value; Value.String newValue] -> Value.String (str.Replace(value, newValue))
                | Function.Replace, [Value.Nothing; _; _] -> Value.Nothing

                | Function.Count, [Value.Map map] -> Value.Number map.Count
                | Function.Count, [Value.List list] -> Value.Number list.Length

                | Function.Not, [Value.Nothing] -> Value.Bool true
                | Function.Not, [Value.Bool bool] -> Value.Bool (not bool)
                | Function.Not, [_] -> Value.Bool false

                | Function.Version, [Value.String str] ->
                    let projectDir =
                        match context.ProjectDir with
                        | Some projectDir -> projectDir
                        | _ -> TerrabuildException.Raise($"Project dir not available in this context.")

                    let projectName = FS.workspaceRelative context.WorkspaceDir projectDir str
                    match context.Versions |> Map.tryFind projectName with
                    | Some version -> Value.String version
                    | _ -> TerrabuildException.Raise($"Unknown project reference '{str}'")

                | Function.Format, values ->
                    let formatValue v =
                        match v with
                        | Value.Nothing -> ""
                        | Value.Bool b -> if b then "true" else "false"
                        | Value.Number n -> $"{n}"
                        | Value.String s -> s
                        | _ -> TerrabuildException.Raise($"Unsupported type for format {v}")

                    values
                    |> List.fold (fun acc value -> $"{acc}{formatValue value}") ""
                    |> Value.String

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
            res, varUsed

    eval expr
