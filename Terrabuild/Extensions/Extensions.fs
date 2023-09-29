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

[<AbstractClass>]
type Extension(projectDir:string, projectFile: string, args: Map<string, string>) =
    abstract Capabilities: Capabilities with get
    abstract Dependencies: string list
    abstract Outputs: string list
    abstract Ignores: string list
    abstract GetStep: action:string * args:Map<string, string> -> Step list
