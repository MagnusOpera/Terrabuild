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
type Context = {
    Directory: string
    With: string option
    CI: bool
}

[<RequireQualifiedAccess>]
type ProjectInfo = {
    ProjectFile: string
    Outputs: Set<string>
    Ignores: Set<string>
    Dependencies: Set<string>
}
