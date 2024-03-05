namespace Terrabuild.Extensibility
open System

[<RequireQualifiedAccess>]
type InitContext = {
    Directory: string
    CI: bool
}

[<RequireQualifiedAccess>]
type ProjectInfo = {
    Properties: Map<string, string>
    Outputs: Set<string>
    Ignores: Set<string>
    Dependencies: Set<string>
}

[<RequireQualifiedAccess>]
type ActionContext = {
    Directory: string
    CI: bool
    Properties: Map<string, string>
    NodeHash: string
    Command: string
    BranchOrTag: string
}

[<Flags>]
type Cacheability =
    | Never = 0
    | Local = 1
    | Remote = 2
    | Always = 3 // Local + Remote

[<RequireQualifiedAccess>]
type Action = {
    Command: string
    Arguments: string
    Cache: Cacheability
}
with
    static member Build cmd args cache = { Command = cmd; Arguments = args; Cache = cache }

