namespace Extensions
open System

[<RequireQualifiedAccess>]
type CommandLine = {
    Command: string
    Arguments: string
}


type IContext =
    abstract Directory: string with get
    abstract With: string option with get
    abstract Shared : bool with get

[<AbstractClass; AllowNullLiteral>]
type StepParameters() =
    member val NodeHash:string = null with get, set

[<AbstractClass>]
type Extension(context: IContext) =
    abstract Container: string option
    abstract Dependencies: string list
    abstract Outputs: string list
    abstract Ignores: string list
    abstract GetStepParameters: action:string -> Type
    abstract BuildStepCommands: action:string * parameters:StepParameters -> CommandLine list
