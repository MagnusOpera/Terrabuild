namespace Terrabuild.Configuration.Workspace.AST
open Terrabuild.Configuration.AST
open Terrabuild.Expressions
open Errors

[<RequireQualifiedAccess>]
type WorkspaceComponents =
    | Space of string
    | Ignores of string list

[<RequireQualifiedAccess>]
type Workspace = {
    Space: string option
    Ignores: Set<string>
}
with
    static member Build components =
        let space =
            match components |> List.choose (function | WorkspaceComponents.Space value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> Some value
            | _ -> TerrabuildException.Raise("multiple space declared")

        let ignores =
            match components |> List.choose (function | WorkspaceComponents.Ignores value -> Some value | _ -> None) with
            | [] -> None
            | [value] -> value |> Set.ofList |> Some
            | _ -> TerrabuildException.Raise("multiple ignores declared")

        { Workspace.Space = space
          Workspace.Ignores = ignores |> Option.defaultValue Set.empty }


[<RequireQualifiedAccess>]
type TargetComponents =
    | DependsOn of string list
    | Rebuild of Expr

[<RequireQualifiedAccess>]
type Target = {
    DependsOn: Set<string>
    Rebuild: Expr
}
with
    static member Build id components =
        let dependsOn =
            match components |> List.choose (function | TargetComponents.DependsOn value -> Some value | _ -> None) with
            | [] -> Set.empty
            | [value] -> value |> Set.ofList
            | _ -> TerrabuildException.Raise("multiple depends_on declared")

        let rebuild =
            match components |> List.choose (function | TargetComponents.Rebuild value -> Some value | _ -> None) with
            | [] -> Expr.Bool false
            | [value] -> value
            | _ -> TerrabuildException.Raise("multiple rebuild declared")

        id, { DependsOn = dependsOn
              Rebuild = rebuild }


[<RequireQualifiedAccess>]
type ConfigurationComponents =
    | Variables of Map<string, Expr>

[<RequireQualifiedAccess>]
type Configuration = {
    Variables: Map<string, Expr>
}
with
    static member Build id components =
        let variables =
            match components |> List.choose (function | ConfigurationComponents.Variables value -> Some value) with
            | [] -> Map.empty
            | [value] -> value
            | _ -> TerrabuildException.Raise("multiple variables declared")

        id, { Variables = variables }

[<RequireQualifiedAccess>]
type WorkspaceFileComponents =
    | Workspace of Workspace
    | Target of string * Target
    | Configuration of string * Configuration
    | Extension of string * Extension

[<RequireQualifiedAccess>]
type WorkspaceFile = {
    Workspace: Workspace
    Targets: Map<string, Target>
    Configurations: Map<string, Configuration>
    Extensions: Map<string, Extension>
}
with
    static member Build components =
        let workspace =
            match components |> List.choose (function | WorkspaceFileComponents.Workspace value -> Some value | _ -> None) with
            | [] ->
                { Workspace.Space = None
                  Workspace.Ignores = Set.empty }
            | [value] -> value
            | _ -> TerrabuildException.Raise("multiple workspace declared")

        let targets =
            components
            |> List.choose (function | WorkspaceFileComponents.Target (name, target) -> Some (name, target) | _ -> None)
            |> Map.ofList

        let configurations =
            components
            |> List.choose (function | WorkspaceFileComponents.Configuration (name, ext) -> Some (name, ext) | _ -> None)
            |> Map.ofList

        let extensions =
            components
            |> List.choose (function | WorkspaceFileComponents.Extension (name, ext) -> Some (name, ext) | _ -> None)
            |> Map.ofList

        { Workspace = workspace
          Targets = targets
          Configurations = configurations
          Extensions = extensions }
