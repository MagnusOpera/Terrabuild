namespace Terrabuild.Extensibility
open System

[<Flags>]
type Cacheability =
    | Never = 0
    | Local = 1
    | Remote = 2
    | Always = 3 // Local + Remote

[<RequireQualifiedAccess>]
type CommandLine = {
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



type ICommandFactory = interface end

type ICommandFactoryParameterless =
    inherit ICommandFactory
    abstract GetSteps: unit -> CommandLine list

type ICommandFactory<'T> =
    inherit ICommandFactory
    abstract GetSteps: arguments:'T -> CommandLine list

type IBuilder =
    abstract Container: string option
    abstract Dependencies: string list
    abstract Outputs: string list
    abstract Ignores: string list
    abstract CreateCommand: action:string -> ICommandFactory

type IExtensionFactory =
    abstract CreateBuilder: Context -> IBuilder
