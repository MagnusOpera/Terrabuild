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
    | Block of name:string * Attributes
    | BlockWithType of name:string * kind:string * Attributes
    | BlockWithTypeAndAlias of name:string * kind:string * alias:string * Attributes

and Attributes = Attribute list
