module Terrabuild.Extensibility
open System

[<RequireQualifiedAccess>]
type ExtensionContext = {
    Debug: bool
    Directory: string
    CI: bool
}

[<RequireQualifiedAccess>]
type ProjectInfo = {
    Outputs: Set<string>
    Ignores: Set<string>
    Dependencies: Set<string>
    Links: Set<string>
    Includes: Set<string>
}
with
    static member Default = {
        Outputs = Set.empty
        Ignores = Set.empty
        Dependencies = Set.empty
        Links = Set.empty
        Includes = Set [ "**/*" ]
    }

[<RequireQualifiedAccess>]
type ActionContext = {
    Debug: bool
    CI: bool
    Command: string
    BranchOrTag: string

    Hash: string
}

[<RequireQualifiedAccess>]
type ShellOperation = {
    Command: string
    Arguments: string
}

[<Flags>]
type Cacheability =
    | Never = 0
    | Local = 1
    | Remote = 2
    | Always = 3 // Local + Remote

type ShellOperations = ShellOperation list

[<RequireQualifiedAccess>]
type ActionExecutionRequest = {
    Cache: Cacheability
    Operations: ShellOperations
}



let shellOp cmd args = 
    { ShellOperation.Command = cmd
      ShellOperation.Arguments = args }

let execRequest cache ops =
    { ActionExecutionRequest.Cache = cache 
      ActionExecutionRequest.Operations = ops }
