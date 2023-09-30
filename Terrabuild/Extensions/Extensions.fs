namespace Extensions
open System

type Step = {
    Command: string
    Arguments: string
}

[<Flags>]
type Capabilities =
    | None = 0
    | Dependencies = 1
    | Steps = 2
    | Outputs = 4
    | Ignores = 8

type IExtensionContext =
    abstract ProjectDirectory: string with get
    abstract ProjectFile: string with get
    abstract Parameters: Map<string, string> with get

[<AbstractClass>]
type Extension(context: IExtensionContext) =
    abstract Capabilities: Capabilities with get
    abstract Dependencies: string list
    abstract Outputs: string list
    abstract Ignores: string list
    abstract GetStep: action:string * args:Map<string, string> -> Step list
