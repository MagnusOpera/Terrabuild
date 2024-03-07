namespace Terrabuild.Configuration.Workspace.AST
open Terrabuild.Configuration.AST

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
    | Target of string * Target
    | Environment of string * Environment
    | Extension of string * Extension

type Workspace = {
    Targets: Map<string, Target>
    Environments: Map<string, Environment>
    Extensions: Map<string, Extension>
}
with
    static member Empty =
        { Targets = Map.empty
          Environments = Map.empty
          Extensions = Map.empty }

    member this.Patch comp =
        match comp with
        | WorkspaceComponents.Target (name, target) -> { this with Targets = this.Targets |> Map.add name target }
        | WorkspaceComponents.Environment (name, environment) -> { this with Environments = this.Environments |> Map.add name environment }
        | WorkspaceComponents.Extension (name, extension) -> { this with Extensions = this.Extensions |> Map.add name extension }
