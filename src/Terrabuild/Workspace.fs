module Workspace
open Mapper

[<RequireQualifiedAccess>]
type Expression =
    | String of string
    | None

type Terrabuild = {
    [<AttributeName("storage")>] Storage: string option
    [<AttributeName("sourcecontrol")>] SourceControl: string option
}

type Target = {
    [<BlockName>] Name: string
    [<AttributeName("depends_on"); Required>] DependsOn: string list
}

type Environment = {
    [<BlockName>] Name: string
    [<AttributeName("variables")>] Variables: Map<string, string>
}

type Extension = {
    [<BlockName>] Name: string
    [<BlockType>] Type: string option
    [<AttributeName("container")>] Container: string option
    [<AttributeName("parameters")>] Parameters: Map<string, Expression>
}

type Workspace = {
    [<Block("terrabuild")>] Terrabuild: Terrabuild option
    [<Block("target")>] Targets: Target list
    [<Block("environment")>] Environments: Environment list
    [<Block("extension")>] Extensions: Extension list
}
