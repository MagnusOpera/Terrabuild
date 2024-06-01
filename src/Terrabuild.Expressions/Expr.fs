namespace Terrabuild.Expressions

[<RequireQualifiedAccessAttribute>]
type Function =
    | Plus
    | Trim
    | Upper
    | Lower
    | Version
    | Item

[<RequireQualifiedAccessAttribute>]
type Expr =
    | Nothing
    | Boolean of value:bool
    | String of value:string
    | Number of value:int
    | Map of Map<string, Expr>
    | List of Expr list
    | Object of obj
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
