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
    ProjectHash: string
}

[<RequireQualifiedAccess>]
type StatusCode =
    | SuccessUpdate
    | Success
    | Error of exitCode:int
with
    member this.IsOkish =
        match this with
        | Success | SuccessUpdate -> true
        | _ -> false

[<RequireQualifiedAccess>]
type ShellOperation = {
    Command: string
    Arguments: string
    ExitCodes: Map<int, StatusCode>
}

[<Flags>]
type Cacheability =
    | Never = 0
    | Local = 1
    | Remote = 2
    | Always = 3 // Local + Remote
    | External = 4 // NOTE: mutually exclusive with Local or Remote

type ShellOperations = ShellOperation list

[<RequireQualifiedAccess>]
type ActionExecutionRequest = {
    Cache: Cacheability
    Operations: ShellOperations
}



let defaultExitCodes = Map [ 0, StatusCode.SuccessUpdate ]

let shellOp cmd args = 
    { ShellOperation.Command = cmd
      ShellOperation.Arguments = args
      ShellOperation.ExitCodes = defaultExitCodes }

let checkOp cmd args exitCodes = 
    { ShellOperation.Command = cmd
      ShellOperation.Arguments = args
      ShellOperation.ExitCodes = exitCodes }

let execRequest cache ops =
    { ActionExecutionRequest.Cache = cache 
      ActionExecutionRequest.Operations = ops }
