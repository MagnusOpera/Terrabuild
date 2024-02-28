namespace Terrabuild.Parser.Build.AST
open Terrabuild.Parser.AST



[<RequireQualifiedAccess>]
type ExtensionComponents =
    | Container of string
    | Script of string
    | Parameters of Map<string, Expr>

type Extension = {
    Container: string option
    Script: string option
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
type ProjectComponents =
    | Dependencies of string list
    | Outputs of string list
    | Ignores of string list
    | Labels of string list
    | Parser of string

type Project = {
    Dependencies: Set<string>
    Outputs: Set<string>
    Ignores: Set<string>
    Labels: Set<string>
    Parser: string option
}
with
    static member Empty =
        { Dependencies = Set.empty
          Outputs = Set.empty
          Ignores = Set.empty
          Labels = Set.empty
          Parser = None }

    member this.Patch comp =
        match comp with
        | ProjectComponents.Dependencies dependencies -> { this with Dependencies = dependencies |> Set.ofList }
        | ProjectComponents.Outputs outputs -> { this with Outputs = outputs |> Set.ofList }
        | ProjectComponents.Ignores ignores -> { this with Ignores = ignores |> Set.ofList }
        | ProjectComponents.Labels labels -> { this with Labels = labels |> Set.ofList }
        | ProjectComponents.Parser parser -> { this with Parser = Some parser }



[<RequireQualifiedAccess>]
type Step = {
    Extension: string
    Command: string
    Parameters: Map<string, Expr>
}

[<RequireQualifiedAccess>]
type TargetComponents =
    | Step of Step

type Target = {
    Steps: Step list
}
with
    static member Empty =
        { Steps = [] }

    member this.Patch comp =
        match comp with
        | TargetComponents.Step step -> { this with Steps = this.Steps @ [step] }

[<RequireQualifiedAccess>]
type BuildComponents =
    | Extension of string * Extension
    | Project of Project
    | Target of string * Target

type Build = {
    Extensions: Map<string, Extension>
    Project: Project
    Targets: Map<string, Target>
}
with
    static member Empty =
        { Extensions = Map.empty
          Project = Project.Empty
          Targets = Map.empty }

    member this.Patch comp =
        match comp with
        | BuildComponents.Extension (name, extension) -> { this with Extensions = this.Extensions |> Map.add name extension }
        | BuildComponents.Project project -> { this with Project = project }
        | BuildComponents.Target (name, target) -> { this with Targets = this.Targets |> Map.add name target }
