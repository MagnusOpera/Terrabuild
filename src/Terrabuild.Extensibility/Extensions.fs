namespace Terrabuild.Extensibility
open System

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

[<RequireQualifiedAccess>]
type InitContext = {
    Directory: string
    CI: bool
}

[<RequireQualifiedAccess>]
type ActionContext = {
    Properties: Map<string, string>
    Directory: string
    CI: bool
    NodeHash: string
    Command: string
    BranchOrTag: string
}

[<RequireQualifiedAccess>]
type ProjectInfo = {
    Properties: Map<string, string>
    Outputs: Set<string>
    Ignores: Set<string>
    Dependencies: Set<string>
}
