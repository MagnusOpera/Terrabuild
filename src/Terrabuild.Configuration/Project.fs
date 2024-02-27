module Project
open Mapper

type Expression = AST.Expr
type Attributes = AST.Attributes

type Extension = {
    [<Kind>] Kind: string
    [<Name("import")>] Import: Expression
    [<Name("container")>] Container: Expression option
    [<Name("parameters")>] Parameters: Map<string, Expression>
}

type Project = {
    [<Name("dependencies")>] Dependencies: Expression list
    [<Name("outputs")>] Outputs: Expression list
    [<Name("labels")>] Labels: Expression list
    [<Name("parser")>] Parser: Expression option
}

type Target = {
    [<Any>] Commands: Attributes
}


type ProjectConfiguration = {
    [<Name("extension")>] Extensions: Map<string, Extension>
    [<Name("project")>] Project: Project option
    [<Name("target")>] Targets: Map<string, Target>
}
