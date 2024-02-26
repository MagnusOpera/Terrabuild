module AST

type Operator =
    | Plus
    | Trim
    | Upper
    | Lower

type Expr =
    | Nothing
    | Boolean of value:bool
    | String of value:string
    | Variable of name:string
    | InfixFunction of Expr * Operator * Expr
    | Function of Operator * Expr

type Attribute =
    | Value of name:string * value:Expr
    | Array of name:string * value:Expr list
    | Block of resource:string * Blocks
    | BlockWithType of resource:string * tpe:string * Blocks
    | BlockWithTypeAndName of resource:string * tpe:string * name:string * Blocks

and Blocks = Attribute list
