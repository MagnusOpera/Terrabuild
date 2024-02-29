namespace Terrabuild.Extensibility
open System

[<Flags>]
type Cacheability =
    | Never = 0
    | Local = 1
    | Remote = 2
    | Always = 3 // Local + Remote

[<RequireQualifiedAccess>]
type Action = {
    Command: string
    Arguments: string
    Cache: Cacheability
}


[<RequireQualifiedAccess>]
type Context = {
    Directory: string
    With: string option
    CI: bool
}


[<RequireQualifiedAccess>]
type ProjectInfo = {
    ProjectFile: string
    Outputs: Set<string>
    Ignores: Set<string>
    Dependencies: Set<string>
}


// type IBaseCommand = interface end

// type ICommand =
//     inherit IBaseCommand
//     abstract CreateSteps: unit -> Step list

// type ICommand<'T> =
//     inherit IBaseCommand
//     abstract CreateSteps: arguments:'T -> Step list

// type IBuilder =
//     abstract Container: string option
//     abstract Dependencies: string list
//     abstract Outputs: string list
//     abstract Ignores: string list
//     abstract CreateCommand: action:string -> IBaseCommand

// type IExtension =
//     abstract CreateBuilder: Context -> IBuilder
