namespace Terrabuild.Expressions

[<RequireQualifiedAccessAttribute>]
type Function =
    | Plus
    | Trim
    | Upper
    | Lower

[<RequireQualifiedAccessAttribute>]
type Expr =
    | Nothing
    | Boolean of value:bool
    | String of value:string
    | Variable of name:string
    | Map of Map<string, Expr>
    | Function of Function * Expr list

