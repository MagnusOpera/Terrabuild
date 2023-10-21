namespace Extensions
open System

type Command = {
    Command: string
    Arguments: string
}

type IContext =
    abstract Directory: string with get
    abstract With: string option with get

[<AbstractClass; AllowNullLiteral>]
type StepParameters() =
    member val NodeHash:string = null with get, set
    member val Shared:bool = false with get, set
    member val Commit:string = "" with get, set
    member val BranchOrTag:string = "" with get, set

[<AbstractClass>]
type Extension(context: IContext) =
    abstract Dependencies: string list
    abstract Outputs: string list
    abstract Ignores: string list
    abstract GetStepParameters: action:string -> Type
    abstract BuildStepCommands: action:string * parameters:StepParameters -> Command list
