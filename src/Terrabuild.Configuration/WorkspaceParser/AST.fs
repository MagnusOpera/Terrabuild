namespace Terrabuild.Configuration.Workspace.AST
open Terrabuild.Configuration.AST
open Terrabuild.Expressions

[<RequireQualifiedAccess>]
type WorkspaceComponents =
    | Space of string

type Workspace = {
    Space: string option
}
with
    static member Empty =
        { Space = None }

    member this.Patch comp =
        match comp with
        | WorkspaceComponents.Space space -> { this with Space = space |> Some }


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
type ContextComponents =
    | Variables of Map<string, Expr>

type Context = {
    Variables: Map<string, Expr>
}
with
    static member Empty =
        { Variables = Map.empty }

    member this.Patch comp =
        match comp with
        | ContextComponents.Variables variables -> { this with Variables = variables }


[<RequireQualifiedAccess>]
type WorkspaceFileComponents =
    | Workspace of Workspace
    | Target of string * Target
    | Configuration of string * Context
    | Environment of string * Context
    | Extension of string * Extension

type WorkspaceFile = {
    Space: string option
    Targets: Map<string, Target>
    Configurations: Map<string, Context>
    Environments: Map<string, Context>
    Extensions: Map<string, Extension>
}
with
    static member Empty =
        { Space = None
          Targets = Map.empty
          Configurations = Map.empty
          Environments = Map.empty
          Extensions = Map.empty }

    member this.Patch comp =
        match comp with
        | WorkspaceFileComponents.Workspace workspace -> { this with Space = workspace.Space }
        | WorkspaceFileComponents.Target (name, target) -> { this with Targets = this.Targets |> Map.add name target }
        | WorkspaceFileComponents.Configuration (name, configuration) -> { this with Configurations = this.Configurations |> Map.add name configuration }
        | WorkspaceFileComponents.Environment (name, environment) -> { this with Environments = this.Environments |> Map.add name environment }
        | WorkspaceFileComponents.Extension (name, extension) -> { this with Extensions = this.Extensions |> Map.add name extension }
