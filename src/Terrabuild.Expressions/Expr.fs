namespace Terrabuild.Expressions
open System

[<RequireQualifiedAccessAttribute>]
type Function =
    | Plus
    | Minus
    | Trim
    | Upper
    | Lower
    | Replace
    | Count
    | Version
    | Format
    | ToString
    | Item
    | TryItem
    | Coalesce
    | Ternary
    | Equal
    | NotEqual
    | Not
    | And
    | Or

[<RequireQualifiedAccessAttribute>]
type Expr =
    | Nothing
    | Bool of value:bool
    | String of value:string
    | Number of value:int
    | Map of Map<string, Expr>
    | List of Expr list
    | Variable of name:string
    | Function of Function * Expr list


[<RequireQualifiedAccess>]
type Value =
    | Nothing
    | Bool of bool
    | String of string
    | Number of int
    | Map of Map<string, Value>
    | List of Value list
    | Object of obj
