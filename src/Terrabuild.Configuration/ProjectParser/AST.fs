module Terrabuild.Configuration.Project.AST
open Terrabuild.Configuration.AST
open Terrabuild.Expressions
open Errors


// NOTE: must be in sync with Terrabuild.Extensibility.Cacheability
[<RequireQualifiedAccess>]
type Cacheability =
    | Never
    | Local
    | Remote
    | Always

[<RequireQualifiedAccess>]
type ProjectComponents =
    | Dependencies of string list
    | Links of string list
    | Outputs of string list
    | Ignores of string list
    | Includes of string list
    | Labels of string list

[<RequireQualifiedAccess>]
type Project = {
    Init: string option
    Dependencies: Set<string>
    Links: Set<string>
    Outputs: Set<string>
    Ignores: Set<string>
    Includes: Set<string>
    Labels: Set<string>
}
with
    static member Build init components =
        let dependencies =
            match components |> List.choose (function | ProjectComponents.Dependencies value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> raiseParseError "multiple dependencies declared"

        let links =
            match components |> List.choose (function | ProjectComponents.Links value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> raiseParseError "multiple links declared"

        let outputs =
            match components |> List.choose (function | ProjectComponents.Outputs value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> raiseParseError "multiple outputs declared"

        let ignores =
            match components |> List.choose (function | ProjectComponents.Ignores value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> raiseParseError "multiple ignores declared"

        let includes =
            match components |> List.choose (function | ProjectComponents.Includes value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> raiseParseError "multiple files declared"

        let labels =
            match components |> List.choose (function | ProjectComponents.Labels value -> Some value | _ -> None) with
            | [] -> Set.empty
            | [value] -> value |> Set.ofList
            | _ -> raiseParseError "multiple labels declared"

        { Init = init
          Dependencies = dependencies |> Option.defaultValue Set.empty
          Links = links |> Option.defaultValue Set.empty
          Outputs = outputs |> Option.defaultValue Set.empty
          Ignores = ignores |> Option.defaultValue Set.empty
          Includes = includes |> Option.defaultValue Set.empty
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
    | Cache of string
    | Step of Step

[<RequireQualifiedAccess>]
type Target = {
    Rebuild: Expr option
    Outputs: Set<string> option
    DependsOn: Set<string> option
    Cache: Cacheability option
    Steps: Step list
}
with
    static member Build id components =
        let dependsOn =
            match components |> List.choose (function | TargetComponents.DependsOn value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> raiseParseError "multiple depends_on declared"

        let rebuild =
            match components |> List.choose (function | TargetComponents.Rebuild value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> Some value
            | _ -> raiseParseError "multiple rebuild declared"

        let outputs =
            match components |> List.choose (function | TargetComponents.Outputs value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> raiseParseError "multiple outputs declared"

        let cache =
            match components |> List.choose (function | TargetComponents.Cache value -> Some value | _ -> None) with
            | [] -> None
            | [value] ->
                // warning this is the values of Terrabuild.Extensibility.Cacheability
                match value with
                | "never" -> Some Cacheability.Never
                | "local" -> Some Cacheability.Local
                | "remote" -> Some Cacheability.Remote
                | "always" -> Some Cacheability.Always
                | _ -> raiseParseError "invalid cache value"
            | _ -> raiseParseError "multiple cache declared"

        let steps =
            components
            |> List.choose (function | TargetComponents.Step step -> Some step | _ -> None)

        id, { DependsOn = dependsOn
              Rebuild = rebuild
              Outputs = outputs
              Cache = cache
              Steps = steps }


[<RequireQualifiedAccess>]
type ProjectFileComponents =
    | Project of Project
    | Extension of string * Extension
    | Target of string * Target

[<RequireQualifiedAccess>]
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
            | _ -> raiseParseError "multiple project declared"

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
