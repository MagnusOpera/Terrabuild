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
    Includes: Set<string>
}
with
    static member Default = {
        Outputs = Set.empty
        Ignores = Set.empty
        Dependencies = Set.empty
        Includes = Set [ "**/*" ]
    }

[<RequireQualifiedAccess>]
type ActionContext = {
    Debug: bool
    CI: bool
    Command: string
    BranchOrTag: string

    UniqueId: string
    TempDir: string
    Projects: Map<string, string>
}

[<RequireQualifiedAccess>]
type ShellOperation = {
    Command: string
    Arguments: string
}

type FunOperation = unit -> unit

[<RequireQualifiedAccess>]
type Operation =
    | Shell of ShellOperation
    | Function of FunOperation

[<Flags>]
type Cacheability =
    | Never = 0
    | Local = 1
    | Remote = 2
    | Always = 3 // Local + Remote

type Operations = Operation list

type ApplyOperations =
    | Each of Map<string, Operations>
    | All of Operations

[<RequireQualifiedAccess>]
type ActionExecutionRequest = {
    Cache: Cacheability
    PreOperations: Operations
    Operations: ApplyOperations
}



let shellOp cmd args = 
    Operation.Shell { ShellOperation.Command = cmd
                      ShellOperation.Arguments = args }

let funOp f =
    Operation.Function f

let execRequest cache preOps ops =
    { ActionExecutionRequest.Cache = cache 
      ActionExecutionRequest.PreOperations = preOps
      ActionExecutionRequest.Operations = ops }
