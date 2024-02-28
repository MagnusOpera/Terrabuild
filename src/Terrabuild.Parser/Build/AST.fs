namespace Terrabuild.Parser.Build.AST
open Terrabuild.Parser.AST



[<RequireQualifiedAccess>]
type ExtensionComponents =
    | Container of string
    | Parameters of Map<string, Expr>

type Extension = {
    Container: string option
    Parameters: Map<string, Expr>
}
with
    static member Empty =
        { Container = None
          Parameters = Map.empty }

    member this.Patch comp =
        match comp with
        | ExtensionComponents.Container container -> { this with Container = Some container }
        | ExtensionComponents.Parameters parameters -> { this with Parameters = parameters }



[<RequireQualifiedAccess>]
type ProjectComponents =
    | Dependencies of string list
    | Outputs of string list
    | Labels of string list
    | Parser of string

type Project = {
    Dependencies: string list
    Outputs: string list
    Labels: string list
    Parser: string option
}
with
    static member Empty =
        { Dependencies = []
          Outputs = [] 
          Labels = []
          Parser = None }

    member this.Patch comp =
        match comp with
        | ProjectComponents.Dependencies dependencies -> { this with Dependencies = dependencies }
        | ProjectComponents.Outputs outputs -> { this with Outputs = outputs }
        | ProjectComponents.Labels labels -> { this with Labels = labels }
        | ProjectComponents.Parser parser -> { this with Parser = Some parser }



[<RequireQualifiedAccess>]
type Command = {
    Extension: string
    Command: string
    Parameters: Map<string, Expr>
}

[<RequireQualifiedAccess>]
type TargetComponents =
    | Command of Command

type Target = {
    Commands: Command list
}
with
    static member Empty =
        { Commands = [] }

    member this.Patch comp =
        match comp with
        | TargetComponents.Command command -> { this with Commands = this.Commands @ [command] }

[<RequireQualifiedAccess>]
type BuildComponents =
    | Extension of string * Extension
    | Project of Project
    | Target of string * Target

type Build = {
    Extensions: Map<string, Extension>
    Project: Project option
    Targets: Map<string, Target>
}
with
    static member Empty =
        { Extensions = Map.empty
          Project = None
          Targets = Map.empty }

    member this.Patch comp =
        match comp with
        | BuildComponents.Extension (name, extension) -> { this with Extensions = this.Extensions |> Map.add name extension }
        | BuildComponents.Project project -> { this with Project = Some project }
        | BuildComponents.Target (name, target) -> { this with Targets = this.Targets |> Map.add name target }
