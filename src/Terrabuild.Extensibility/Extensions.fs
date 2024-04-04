module Terrabuild.Extensibility
open System

[<RequireQualifiedAccess>]
type InitContext = {
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
type ActionBatch = {
    Cache: Cacheability
    Actions: Action list
    Bulkable: bool
}

let scope cache =
    { ActionBatch.Cache = cache
      ActionBatch.Actions = []
      ActionBatch.Bulkable = false }

let andThen cmd args (batch: ActionBatch) =
    let action = action cmd args
    { batch with
        ActionBatch.Actions = batch.Actions @ [ action ] }

let andIf predicat (action: ActionBatch -> ActionBatch) (batch: ActionBatch) =
    if predicat then action batch
    else batch

let bulkable (batch: ActionBatch) =
    { batch with Bulkable = true }
