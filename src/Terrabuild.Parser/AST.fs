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

type Block = {
    Kind: string option
    Alias: string option
    Attributes: Attributes
}

and Value =
    | Scalar of Expr
    | Array of Expr list
    | Block of Block

and Attribute = {
    Name: string
    Value: Value
}

and Attributes = Attribute list
