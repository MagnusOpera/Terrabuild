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



type ICommandFactory =
    abstract TypeOfArguments: Type option
    abstract GetSteps: arguments:obj -> CommandLine list

type IBuilder =
    abstract Container: string option
    abstract Dependencies: string list
    abstract Outputs: string list
    abstract Ignores: string list
    abstract CreateCommand: action:string -> ICommandFactory

type IExtensionFactory =
    abstract CreateBuilder: Context -> IBuilder
