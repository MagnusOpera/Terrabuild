module Terrabuild.Configuration.Project.AST
open Terrabuild.Configuration.AST
open Terrabuild.Expressions

[<RequireQualifiedAccess>]
type ProjectComponents =
    | Dependencies of string list
    | Outputs of string list
    | Ignores of string list
    | Files of string list
    | Labels of string list
    | Init of string

type Project = {
    Dependencies: Set<string> option
    Outputs: Set<string> option
    Ignores: Set<string> option
    Files: Set<string> option
    Labels: Set<string>
    Init: string option
}
with
    static member Empty =
        { Dependencies = None
          Outputs = None
          Ignores = None
          Files = None
          Labels = Set.empty
          Init = None }

    member this.Patch comp =
        match comp with
        | ProjectComponents.Dependencies dependencies -> { this with Dependencies = dependencies |> Set.ofList |> Some }
        | ProjectComponents.Outputs outputs -> { this with Outputs = outputs |> Set.ofList |> Some }
        | ProjectComponents.Ignores ignores -> { this with Ignores = ignores |> Set.ofList |> Some }
        | ProjectComponents.Files files -> { this with Files = files |> Set.ofList |> Some }
        | ProjectComponents.Labels labels -> { this with Labels = labels |> Set.ofList |> Set.map (fun x -> x.ToLowerInvariant()) }
        | ProjectComponents.Init init -> { this with Init = Some init }



type Step = {
    Extension: string
    Command: string
    Parameters: Map<string, Expr>
}

[<RequireQualifiedAccess>]
type TargetComponents =
    | DependsOn of string list
    | Rebuild of bool
    | Outputs of string list
    | Step of Step

type Target = {
    DependsOn: Set<string> option
    Rebuild: bool option
    Outputs: Set<string> option
    Steps: Step list
}
with
    static member Empty =
        { DependsOn = None
          Rebuild = None
          Outputs = None
          Steps = [] }

    member this.Patch comp =
        match comp with
        | TargetComponents.DependsOn dependsOn -> { this with DependsOn = dependsOn |> Set.ofList |> Some }
        | TargetComponents.Rebuild rebuild -> { this with Rebuild = Some rebuild }
        | TargetComponents.Outputs outputs -> { this with Outputs = outputs |> Set.ofList |> Some }
        | TargetComponents.Step step -> { this with Steps = this.Steps @ [step] }

[<RequireQualifiedAccess>]
type ProjectFileComponents =
    | Project of Project
    | Extension of string * Extension
    | Target of string * Target

type ProjectFile = {
    Project: Project
    Extensions: Map<string, Extension>
    Targets: Map<string, Target>
}
with
    static member Empty =
        { Extensions = Map.empty
          Project = Project.Empty
          Targets = Map.empty }

    member this.Patch comp =
        match comp with
        | ProjectFileComponents.Project project -> { this with Project = project }
        | ProjectFileComponents.Extension (name, extension) -> { this with Extensions = this.Extensions |> Map.add name extension }
        | ProjectFileComponents.Target (name, target) -> { this with Targets = this.Targets |> Map.add name target }
