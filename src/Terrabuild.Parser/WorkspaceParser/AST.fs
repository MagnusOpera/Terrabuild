namespace Terrabuild.Parser.Workspace.AST
open Terrabuild.Parser.AST
open Terrabuild.Expressions

[<RequireQualifiedAccess>]
type ConfigurationComponents =
    | Storage of string
    | SourceControl of string

type Configuration = {
    Storage: string option
    SourceControl: string option
}
with
    static member Empty =
        { Storage = None
          SourceControl = None }    

    member this.Patch comp =
        match comp with
        | ConfigurationComponents.Storage storage -> { this with Storage = Some storage }
        | ConfigurationComponents.SourceControl sourceControl -> { this with SourceControl = Some sourceControl }


[<RequireQualifiedAccess>]
type TargetComponents =
    | DependsOn of string list

type Target = {
    DependsOn: Set<string>
}
with
    static member Empty =
        { DependsOn = Set.empty }

    member this.Patch comp =
        match comp with
        | TargetComponents.DependsOn dependsOn -> { this with DependsOn = dependsOn |> Set.ofList }


[<RequireQualifiedAccess>]
type EnvironmentComponents =
    | Variables of Map<string, string>

type Environment = {
    Variables: Map<string, string>
}
with
    static member Empty =
        { Variables = Map.empty }

    member this.Patch comp =
        match comp with
        | EnvironmentComponents.Variables variables -> { this with Variables = variables }


[<RequireQualifiedAccess>]
type WorkspaceComponents =
    | Configuration of Configuration
    | Target of string * Target
    | Environment of string * Environment
    | Extension of string * Extension

type Workspace = {
    Configuration: Configuration
    Targets: Map<string, Target>
    Environments: Map<string, Environment>
    Extensions: Map<string, Extension>
}
with
    static member Empty =
        { Configuration = Configuration.Empty
          Targets = Map.empty
          Environments = Map.empty
          Extensions = Map.empty }

    member this.Patch comp =
        match comp with
        | WorkspaceComponents.Configuration configuration -> { this with Configuration = configuration }
        | WorkspaceComponents.Target (name, target) -> { this with Targets = this.Targets |> Map.add name target }
        | WorkspaceComponents.Environment (name, environment) -> { this with Environments = this.Environments |> Map.add name environment }
        | WorkspaceComponents.Extension (name, extension) -> { this with Extensions = this.Extensions |> Map.add name extension }
