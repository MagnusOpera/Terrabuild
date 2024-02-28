namespace Terrabuild.Parser.Workspace.AST
open Terrabuild.Parser.AST

[<RequireQualifiedAccess>]
type TerrabuildComponents =
    | Storage of string
    | SourceControl of string

type Terrabuild = {
    Storage: string option
    SourceControl: string option
}
with
    static member Empty =
        { Storage = None
          SourceControl = None }    

    member this.Patch comp =
        match comp with
        | TerrabuildComponents.Storage storage -> { this with Storage = Some storage }
        | TerrabuildComponents.SourceControl sourceControl -> { this with SourceControl = Some sourceControl }


[<RequireQualifiedAccess>]
type TargetComponents =
    | DependsOn of string list

type Target = {
    DependsOn: string list
}
with
    static member Empty =
        { DependsOn = [] }

    member this.Patch comp =
        match comp with
        | TargetComponents.DependsOn dependsOn -> { this with DependsOn = dependsOn }


[<RequireQualifiedAccess>]
type EnvironmentComponents =
    | Variables of Map<string, Expr>

type Environment = {
    Variables: Map<string, Expr>
}
with
    static member Empty =
        { Variables = Map.empty }

    member this.Patch comp =
        match comp with
        | EnvironmentComponents.Variables variables -> { this with Variables = variables }


[<RequireQualifiedAccess>]
type ExtensionComponents =
    | Container of string
    | Parameters of Map<string, Expr>
    | Script of string

type Extension = {
    Script: string option
    Container: string option
    Parameters: Map<string, Expr>
}
with
    static member Empty =
        { Container = None
          Script = None
          Parameters = Map.empty }

    member this.Patch comp =
        match comp with
        | ExtensionComponents.Container container -> { this with Container = Some container }
        | ExtensionComponents.Script script -> { this with Script = Some script }
        | ExtensionComponents.Parameters parameters -> { this with Parameters = parameters }


[<RequireQualifiedAccess>]
type WorkspaceComponents =
    | Terrabuild of Terrabuild
    | Target of string * Target
    | Environment of string * Environment
    | Extension of string * Extension

type Workspace = {
    Terrabuild: Terrabuild
    Targets: Map<string, Target>
    Environments: Map<string, Environment>
    Extensions: Map<string, Extension>
}
with
    static member Empty =
        { Terrabuild = Terrabuild.Empty
          Targets = Map.empty
          Environments = Map.empty
          Extensions = Map.empty }

    member this.Patch comp =
        match comp with
        | WorkspaceComponents.Terrabuild terrabuild -> { this with Terrabuild = terrabuild }
        | WorkspaceComponents.Target (name, target) -> { this with Targets = this.Targets |> Map.add name target }
        | WorkspaceComponents.Environment (name, environment) -> { this with Environments = this.Environments |> Map.add name environment }
        | WorkspaceComponents.Extension (name, extension) -> { this with Extensions = this.Extensions |> Map.add name extension }
