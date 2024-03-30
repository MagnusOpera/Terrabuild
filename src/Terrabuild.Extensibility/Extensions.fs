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
    BranchOrTag: string
    BulkParameters: string list
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
    { Action.Command = cmd; Action.Arguments = args }

[<RequireQualifiedAccess>]
type ActionBatch = {
    Cache: Cacheability
    Actions: Action list
    BulkParameter: string option
}

let scope cache =
    { ActionBatch.Cache = cache
      ActionBatch.Actions = []
      ActionBatch.BulkParameter = None }

let andThen cmd args (batch: ActionBatch) =
    let action = action cmd args
    { batch with
        ActionBatch.Actions = batch.Actions @ [ action ] }

let andIf predicat (action: ActionBatch -> ActionBatch) (batch: ActionBatch) =
    if predicat then action batch
    else batch

let withBulk parameter (batch: ActionBatch) =
    { batch with
        BulkParameter = Some parameter }
