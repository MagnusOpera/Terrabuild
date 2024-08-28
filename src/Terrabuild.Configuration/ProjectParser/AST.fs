module Terrabuild.Configuration.Project.AST
open Terrabuild.Configuration.AST
open Terrabuild.Expressions
open Errors

[<RequireQualifiedAccess>]
type ProjectComponents =
    | Dependencies of string list
    | Links of string list
    | Outputs of string list
    | Ignores of string list
    | Includes of string list
    | Labels of string list

type Project = {
    Init: string option
    Dependencies: Set<string> option
    Links: Set<string> option
    Outputs: Set<string> option
    Ignores: Set<string> option
    Includes: Set<string> option
    Labels: Set<string>
}
with
    static member Build init components =
        let dependencies =
            match components |> List.choose (function | ProjectComponents.Dependencies value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> TerrabuildException.Raise("multiple dependencies declared")

        let links =
            match components |> List.choose (function | ProjectComponents.Links value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> TerrabuildException.Raise("multiple links declared")

        let outputs =
            match components |> List.choose (function | ProjectComponents.Outputs value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> TerrabuildException.Raise("multiple outputs declared")

        let ignores =
            match components |> List.choose (function | ProjectComponents.Ignores value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> TerrabuildException.Raise("multiple ignores declared")

        let includes =
            match components |> List.choose (function | ProjectComponents.Includes value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> TerrabuildException.Raise("multiple files declared")

        let labels =
            match components |> List.choose (function | ProjectComponents.Labels value -> Some value | _ -> None) with
            | [] -> Set.empty
            | [value] -> value |> Set.ofList
            | _ -> TerrabuildException.Raise("multiple labels declared")

        { Init = init
          Dependencies = dependencies
          Links = links
          Outputs = outputs
          Ignores = ignores
          Includes = includes
          Labels = labels }
  




type Step = {
    Extension: string
    Command: string
    Parameters: Map<string, Expr>
}

[<RequireQualifiedAccess>]
type TargetComponents =
    | DependsOn of string list
    | Rebuild of Expr
    | Outputs of string list
    | Step of Step

type Target = {
    Rebuild: Expr option
    Outputs: Set<string> option
    DependsOn: Set<string> option
    Steps: Step list
}
with
    static member Build id components =
        let dependsOn =
            match components |> List.choose (function | TargetComponents.DependsOn value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> TerrabuildException.Raise("multiple depends_on declared")

        let rebuild =
            match components |> List.choose (function | TargetComponents.Rebuild value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> Some value
            | _ -> TerrabuildException.Raise("multiple rebuild declared")

        let outputs =
            match components |> List.choose (function | TargetComponents.Outputs value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> TerrabuildException.Raise("multiple outputs declared")

        let steps =
            components
            |> List.choose (function | TargetComponents.Step step -> Some step | _ -> None)

        id, { DependsOn = dependsOn
              Rebuild = rebuild
              Outputs = outputs
              Steps = steps }


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
    static member Build components =
        let project =
            match components |> List.choose (function | ProjectFileComponents.Project value -> Some value | _ -> None) with
            | [] -> Project.Build None []
            | [value] -> value
            | _ -> TerrabuildException.Raise("multiple project declared")

        let extensions =
            components
            |> List.choose (function | ProjectFileComponents.Extension (name, ext) -> Some (name, ext) | _ -> None)
            |> Map.ofList

        let targets =
            components
            |> List.choose (function | ProjectFileComponents.Target (name, target) -> Some (name, target) | _ -> None)
            |> Map.ofList

        { Project = project
          Extensions = extensions
          Targets = targets }
