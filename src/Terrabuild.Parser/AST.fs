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

type BlockHeader =
    | Block of resource:string
    | BlockName of resource:string * name:string
    | BlockTypeName of resource:string * tpe:string * name:string

type Attribute =
    | Value of name:string * value:Expr
    | Array of name:string * value:Expr list
    | SubBlock of Block

and Block = {
    Header: BlockHeader
    Body: BlockBody
}

and BlockBody = Attribute list

type Blocks = Block list
