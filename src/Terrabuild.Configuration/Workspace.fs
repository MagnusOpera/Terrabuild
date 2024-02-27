module Workspace
open Mapper

type Expression = AST.Expr

type Terrabuild = {
    [<Name("storage")>] Storage: Expression option
    [<Name("sourcecontrol")>] SourceControl: Expression option
}

type Target = {
    [<Kind>] Kind: string
    [<Name("depends_on")>] DependsOn: Expression list
}

type Environment = {
    [<Kind>] Kind: string
    [<Name("variables")>] Variables: Map<string, Expression>
}

type Extension = {
    [<Kind>] Kind: string
    [<Name("container")>] Container: Expression option
    [<Name("parameters")>] Parameters: Map<string, Expression>
}

type WorkspaceConfiguration = {
    [<Name("terrabuild")>] Terrabuild: Terrabuild
    [<Name("target")>] Targets: Map<string, Target>
    [<Name("environment")>] Environments: Map<string, Environment>
    [<Name("extension")>] Extensions: Map<string, Extension>
}
