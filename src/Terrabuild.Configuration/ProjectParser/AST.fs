module Terrabuild.Configuration.Project.AST
open Terrabuild.Configuration.AST
open Terrabuild.Expressions

[<RequireQualifiedAccess>]
type ConfigurationComponents =
    | Dependencies of string list
    | Outputs of string list
    | Ignores of string list
    | Labels of string list
    | Init of string

type Configuration = {
    Dependencies: Set<string>
    Outputs: Set<string>
    Ignores: Set<string>
    Labels: Set<string>
    Init: string option
}
with
    static member Empty =
        { Dependencies = Set.empty
          Outputs = Set.empty
          Ignores = Set.empty
          Labels = Set.empty
          Init = None }

    member this.Patch comp =
        match comp with
        | ConfigurationComponents.Dependencies dependencies -> { this with Dependencies = dependencies |> Set.ofList }
        | ConfigurationComponents.Outputs outputs -> { this with Outputs = outputs |> Set.ofList }
        | ConfigurationComponents.Ignores ignores -> { this with Ignores = ignores |> Set.ofList }
        | ConfigurationComponents.Labels labels -> { this with Labels = labels |> Set.ofList |> Set.map (fun x -> x.ToLowerInvariant()) }
        | ConfigurationComponents.Init init -> { this with Init = Some init }



type Step = {
    Extension: string
    Command: string
    Parameters: Map<string, Expr>
}

[<RequireQualifiedAccess>]
type TargetComponents =
    | DependsOn of string list
    | Step of Step

type Target = {
    DependsOn: Set<string> option
    Steps: Step list
}
with
    static member Empty =
        { DependsOn = None
          Steps = [] }

    member this.Patch comp =
        match comp with
        | TargetComponents.DependsOn dependsOn -> { this with DependsOn = dependsOn |> Set.ofList |> Some }
        | TargetComponents.Step step -> { this with Steps = this.Steps @ [step] }

[<RequireQualifiedAccess>]
type ProjectComponents =
    | Extension of string * Extension
    | Configuration of Configuration
    | Target of string * Target

type Project = {
    Extensions: Map<string, Extension>
    Configuration: Configuration
    Targets: Map<string, Target>
}
with
    static member Empty =
        { Extensions = Map.empty
          Configuration = Configuration.Empty
          Targets = Map.empty }

    member this.Patch comp =
        match comp with
        | ProjectComponents.Extension (name, extension) -> { this with Extensions = this.Extensions |> Map.add name extension }
        | ProjectComponents.Configuration configuration -> { this with Configuration = configuration }
        | ProjectComponents.Target (name, target) -> { this with Targets = this.Targets |> Map.add name target }
