namespace Extensions
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


type IContext =
    abstract Directory: string with get
    abstract With: string option with get
    abstract CI : bool with get

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
