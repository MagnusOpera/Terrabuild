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
    Properties: Map<string, string>
    Outputs: Set<string>
    Ignores: Set<string>
    Dependencies: Set<string>
}
with
    static member Default = {
        Properties = Map.empty
        Outputs = Set.empty
        Ignores = Set.empty
        Dependencies = Set.empty
    }

[<RequireQualifiedAccess>]
type ActionContext = {
    Debug: bool
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
}

[<RequireQualifiedAccess>]
type ActionBatch = {
    Cache: Cacheability
    Actions: Action list
}

let scope cache =
    { ActionBatch.Cache = cache
      ActionBatch.Actions = [] }

let andThen cmd args (batch: ActionBatch) =
    { batch
      with ActionBatch.Actions = batch.Actions @ [ { Action.Command = cmd; Action.Arguments = args } ]}

let andIf predicat (action: ActionBatch -> ActionBatch) (batch: ActionBatch) =
    if predicat then action batch
    else batch
