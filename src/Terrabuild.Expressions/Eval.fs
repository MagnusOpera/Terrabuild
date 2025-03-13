module Terrabuild.Expressions.Eval
open Terrabuild.Expressions
open Errors
open Collections

type EvaluationContext = {
    WorkspaceDir: string
    ProjectDir: string option
    Versions: Map<string, string>
    Variables: Map<string, Value>
}

let rec eval (context: EvaluationContext) (expr: Expr) =
    let valueToString v =
        match v with
        | Value.Nothing -> ""
        | Value.Bool b -> if b then "true" else "false"
        | Value.Number n -> $"{n}"
        | Value.String s -> s
        | _ -> TerrabuildException.Raise($"Unsupported type for format {v}")

    let rec eval (expr: Expr) =
        match expr with
        | Expr.Nothing -> Value.Nothing
        | Expr.Bool bool -> Value.Bool bool
        | Expr.String str -> Value.String str
        | Expr.Number num -> Value.Number num
        | Expr.Object obj -> Value.Object obj
        | Expr.Variable var ->
            // if varUsed |> Set.contains var then TerrabuildException.Raise($"Variable {var} has circular definition")
            match context.Variables |> Map.tryFind var with
            | Some value -> value
            | None -> TerrabuildException.Raise($"Variable '{var}' is not defined")
        | Expr.Map map ->
            let values = map |> Map.fold (fun map k v ->
                let mv = eval v
                map |> Map.add k mv) Map.empty
            Value.Map values
        | Expr.List list ->
            let values = list |> List.fold (fun list v ->
                let mv = eval v
                list @ [mv]) []
            Value.List values
        | Expr.Function (f, exprs) ->
            let values = exprs |> List.fold (fun exprs expr ->
                let e = eval expr
                exprs @ [e]) []
            let res =
                match f, values with
                | Function.Plus, [Value.String left; Value.String right] -> Value.String (left + right)
                | Function.Upper, [Value.String str] -> Value.String (str.ToUpperInvariant())
                | Function.Trim, [Value.String str] -> Value.String (str.Trim())
                | Function.Lower, [Value.String str] -> Value.String (str.ToLowerInvariant())
                | Function.Replace, [Value.String str; Value.String value; Value.String newValue] -> Value.String (str.Replace(value, newValue))

                | Function.Plus, [Value.Number left; Value.Number right] -> Value.Number (left + right)
                | Function.Minus, [Value.Number left; Value.Number right] -> Value.Number (left - right)

                | Function.Plus, [Value.Map left; Value.Map right] -> Value.Map (left |> Map.addMap right)
                | Function.Count, [Value.Map map] -> Value.Number map.Count

                | Function.Plus, [Value.List left; Value.List right] -> Value.List (left @ right)
                | Function.Count, [Value.List list] -> Value.Number list.Length

                | Function.Not, [Value.Nothing] -> Value.Bool true
                | Function.Not, [Value.Bool bool] -> Value.Bool (not bool)
                | Function.Not, [_] -> Value.Bool false

                | Function.And, [Value.Bool left; Value.Bool right] -> Value.Bool (left && right)
                | Function.Or, [Value.Bool left; Value.Bool right] -> Value.Bool (left || right)

                | Function.Version, [Value.String str] ->
                    let projectDir =
                        match context.ProjectDir with
                        | Some projectDir -> projectDir
                        | _ -> TerrabuildException.Raise($"Project dir not available in this context.")

                    let projectName =
                        FS.workspaceRelative context.WorkspaceDir projectDir str
                        |> String.toUpper
                    match context.Versions |> Map.tryFind projectName with
                    | Some version -> Value.String version
                    | _ -> TerrabuildException.Raise($"Unknown project reference '{str}'")

                | Function.ToString, [value] -> valueToString value |> Value.String

                | Function.Format, [Value.String template; Value.Map values] ->
                    let rec replaceAll template =
                        match template with
                        | String.Regex "{([^}]+)}" [name] ->
                            let value =
                                match values |> Map.tryFind name with
                                | Some value -> valueToString value
                                | _ -> TerrabuildException.Raise($"Field {name} does not exist")
                            template
                            |> String.replace $"{{{name}}}" value
                            |> replaceAll
                        | _ -> template

                    replaceAll template |> Value.String

                | Function.Format, Value.String template :: values ->
                    let values = values |> List.map valueToString

                    let rec replaceAll template =
                        match template with
                        | String.Regex "{([^}]+)}" [index] ->
                            let value =
                                match System.Int32.TryParse index with
                                | (true, index) -> 
                                    if 0 <= index && index < values.Length then values[index]
                                    else TerrabuildException.Raise($"Format index is out of range")
                                | _ -> TerrabuildException.Raise($"Format index is not a number")
                            template
                            |> String.replace $"{{{index}}}" value
                            |> replaceAll
                        | _ -> template

                    replaceAll template |> Value.String

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
            res

    eval expr
