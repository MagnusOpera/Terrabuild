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
}
with
    static member Default = {
        Outputs = Set.empty
        Ignores = Set.empty
        Dependencies = Set.empty
    }

[<RequireQualifiedAccess>]
type ActionContext = {
    Debug: bool
    Directory: string
    CI: bool
    NodeHash: string
    Command: string
    BranchOrTag: string
}


[<RequireQualifiedAccess>]
type OptimizeContext = {
    Debug: bool
    CI: bool
    TempDir: string
    NodeHash: string
    BranchOrTag: string
    ProjectPaths: string list
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
}

let action cmd args = 
    { Action.Command = cmd
      Action.Arguments = args }

[<RequireQualifiedAccess>]
type ActionSequence = {
    Cache: Cacheability
    Actions: Action list
    Batchable: bool
}

let scope cache =
    { ActionSequence.Cache = cache
      ActionSequence.Actions = []
      ActionSequence.Batchable = false }

let andThen cmd args (batch: ActionSequence) =
    let action = action cmd args
    { batch with
        ActionSequence.Actions = batch.Actions @ [ action ] }

let andIf predicat (action: ActionSequence -> ActionSequence) (batch: ActionSequence) =
    if predicat then action batch
    else batch

let batchable (batch: ActionSequence) =
    { batch with Batchable = true }
