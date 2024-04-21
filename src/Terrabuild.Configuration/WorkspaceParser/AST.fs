namespace Terrabuild.Configuration.Workspace.AST
open Terrabuild.Configuration.AST

[<RequireQualifiedAccess>]
type TargetComponents =
    | DependsOn of string list
    | Rebuild of bool

type Target = {
    DependsOn: Set<string> option
    Rebuild: bool option
}
with
    static member Empty =
        { DependsOn = None
          Rebuild = None }

    member this.Patch comp =
        match comp with
        | TargetComponents.DependsOn dependsOn -> { this with DependsOn = dependsOn |> Set.ofList |> Some }
        | TargetComponents.Rebuild rebuild -> { this with Rebuild = rebuild |> Some }


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
type ConfigurationComponents =
    | Space of string

type Configuration = {
    Space: string option
}
with
    static member Empty =
        { Space = None }

    member this.Patch comp =
        match comp with
        | ConfigurationComponents.Space space -> { this with Space = space |> Some }


[<RequireQualifiedAccess>]
type WorkspaceComponents =
    | Configuration of Configuration
    | Target of string * Target
    | Environment of string * Environment
    | Extension of string * Extension

type Workspace = {
    Space: string option
    Targets: Map<string, Target>
    Environments: Map<string, Environment>
    Extensions: Map<string, Extension>
}
with
    static member Empty =
        { Space = None
          Targets = Map.empty
          Environments = Map.empty
          Extensions = Map.empty }

    member this.Patch comp =
        match comp with
        | WorkspaceComponents.Configuration configuration -> { this with Space = configuration.Space }
        | WorkspaceComponents.Target (name, target) -> { this with Targets = this.Targets |> Map.add name target }
        | WorkspaceComponents.Environment (name, environment) -> { this with Environments = this.Environments |> Map.add name environment }
        | WorkspaceComponents.Extension (name, extension) -> { this with Extensions = this.Extensions |> Map.add name extension }
