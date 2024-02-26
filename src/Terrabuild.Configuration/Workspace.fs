module Workspace
open Mapper

[<RequireQualifiedAccess>]
type Expression =
    | String of string
    | None

type Terrabuild = {
    [<Name("storage")>] Storage: string option
    [<Name("sourcecontrol")>] SourceControl: string option
}

type Target = {
    [<Kind>] Kind: string
    [<Name("depends_on")>] DependsOn: string list
}

type Environment = {
    [<Kind>] Kind: string
    [<Name("variables")>] Variables: Map<string, string>
}

type Extension = {
    [<Kind>] Kind: string
    [<Alias>] Alias: string option
    [<Name("container")>] Container: string option
    [<Name("parameters")>] Parameters: Map<string, Expression>
}

type Workspace = {
    [<Name("terrabuild")>] Terrabuild: Terrabuild
    [<Name("target")>] Targets: Target list
    [<Name("environment")>] Environments: Environment list
    [<Name("extension")>] Extensions: Extension list
}
