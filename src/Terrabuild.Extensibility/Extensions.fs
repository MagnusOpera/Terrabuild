namespace Terrabuild.Extensibility
open System

[<Flags>]
type Cacheability =
    | Never = 0
    | Local = 1
    | Remote = 2
    | Always = 3 // Local + Remote

[<RequireQualifiedAccess>]
type Step = {
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



type ICommand = interface end

type ICommandBuilder =
    inherit ICommand
    abstract GetSteps: unit -> Step list

type ICommandBuilder<'T> =
    inherit ICommand
    abstract GetSteps: arguments:'T -> Step list

type IBuilder =
    abstract Container: string option
    abstract Dependencies: string list
    abstract Outputs: string list
    abstract Ignores: string list
    abstract CreateCommand: action:string -> ICommand

type IExtensionFactory =
    abstract CreateBuilder: Context -> IBuilder
