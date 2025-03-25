module Helpers
open Terrabuild.Expressions
open Errors

let parseFunction expr = function
    | "trim" -> Expr.Function (Function.Trim, expr)
    | "upper" -> Expr.Function (Function.Upper, expr)
    | "lower" -> Expr.Function (Function.Lower, expr)
    | "replace" -> Expr.Function (Function.Replace, expr)
    | "count" -> Expr.Function (Function.Count, expr)
    | "version" -> Expr.Function (Function.Version, expr)
    | "format" -> Expr.Function (Function.Format, expr)
    | "tostring" -> Expr.Function (Function.ToString, expr)
    | s -> raiseParseError $"Unknown function: {s}"

let parseExpressionIdentifier = function
    | "true" -> Expr.Bool true
    | "false" -> Expr.Bool false
    | "nothing" -> Expr.Nothing
    | s -> Expr.Variable s

let parseResourceName = function
    | String.Regex "(@?[a-z](?:[_-]?[a-z0-9]+)*)" [identifier] -> identifier
    | s -> raiseParseError $"Invalid resource name: {s}"

let parseResourceIdentifier = function
    | String.Regex "([a-z](?:[_-]?[a-z0-9]+)*)" [identifier] -> identifier
    | s -> raiseParseError $"Invalid resource identifier: {s}"

let parseAttributeName = function
    | String.Regex "([a-z](?:[_-]?[a-z0-9]+)*)" [identifier] -> identifier
    | s -> raiseParseError $"Invalid attribute name: {s}"
