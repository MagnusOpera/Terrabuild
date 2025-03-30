module AST.Helpers
open Terrabuild.Expressions
open Errors

let parseFunction expr = function
    | "trim" -> Expr.Function (Function.Trim, expr)
    | "upper" -> Expr.Function (Function.Upper, expr)
    | "lower" -> Expr.Function (Function.Lower, expr)
    | "replace" -> Expr.Function (Function.Replace, expr)
    | "count" -> Expr.Function (Function.Count, expr)
    | "format" -> Expr.Function (Function.Format, expr)
    | "tostring" -> Expr.Function (Function.ToString, expr)
    | s -> raiseParseError $"Unknown function: {s}"

let parseExpressionIdentifier = function
    | "true" -> Expr.Bool true
    | "false" -> Expr.Bool false
    | "nothing" -> Expr.Nothing
    | s -> Expr.Variable s

let (|RegularIdentifier|ExtensionIdentifier|TargetIdentifier|) (value: string) =
    match value[0] with
    | '@' -> ExtensionIdentifier
    | '^' -> TargetIdentifier
    | _ -> RegularIdentifier

let parseResourceName s =
    match s with
    | ExtensionIdentifier | RegularIdentifier -> s
    | _ -> raiseParseError $"Invalid resource name: {s}"

let parseResourceIdentifier s =
    match s with
    | ExtensionIdentifier | RegularIdentifier -> s
    | _ -> raiseParseError $"Invalid resource identifier: {s}"

let parseAttributeName s =
    match s with
    | RegularIdentifier -> s
    | s -> raiseParseError $"Invalid attribute name: {s}"

let parseRegularIdentifier s =
    match s with
    | RegularIdentifier -> s
    | s -> raiseParseError $"Invalid identifier name: {s}"
