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
    let rec eval (expr: Expr) =
        match expr with
        | Expr.Nothing -> Value.Nothing
        | Expr.Boolean bool -> Value.Bool bool
        | Expr.String str -> Value.String str
        | Expr.Number num -> Value.Number num
        | Expr.Object obj -> Value.Object obj
        | Expr.Variable var ->
            match context.Variables |> Map.tryFind var with
            | None -> TerrabuildException.Raise $"Variable '{var}' is not defined"
            | Some value -> eval value
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
                let projectName = FS.workspaceRelative context.WorkspaceDir context.ProjectDir str
                match context.Versions |> Map.tryFind projectName with
                | Some version -> Value.String version
                | _ -> TerrabuildException.Raise $"Unknown project reference {str}"

            | _ -> TerrabuildException.Raise $"Invalid arguments for function {f}"

    eval expr
