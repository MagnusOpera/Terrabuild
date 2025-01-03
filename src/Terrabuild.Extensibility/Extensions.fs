module Terrabuild.Extensibility
open System

[<RequireQualifiedAccess>]
type ExtensionContext =
    { Debug: bool
      Directory: string
      CI: bool }

[<RequireQualifiedAccess>]
type ProjectInfo =
    { Outputs: Set<string>
      Ignores: Set<string>
      Dependencies: Set<string>
      Links: Set<string>
      Includes: Set<string> }
with
    static member Default =
        { Outputs = Set.empty
          Ignores = Set.empty
          Dependencies = Set.empty
          Links = Set.empty
          Includes = Set [ "**/*" ] }

[<RequireQualifiedAccess>]
type ActionContext =
    { Debug: bool
      CI: bool
      Command: string
      Hash: string }


[<RequireQualifiedAccess>]
type ShellOperation =
    { Command: string
      Arguments: string }

[<Flags>]
type Cacheability =
    | Never = 0
    | Local = 1
    | Remote = 2
    | Always = 3 // Local + Remote

[<RequireQualifiedAccess>]
type ActionExecutionRequest =
    { Cache: Cacheability
      Fingerprints: ShellOperation list
      Operations: ShellOperation list }

let shellOp cmd args =
    { ShellOperation.Command = cmd
      ShellOperation.Arguments = args }


let buildCommand cache =
    { ActionExecutionRequest.Cache = cache
      ActionExecutionRequest.Fingerprints = [] 
      ActionExecutionRequest.Operations = [] }

let withFingerprint fingerprint request =
    { request with 
        ActionExecutionRequest.Fingerprints = fingerprint }

let withOperations operations request =
    { request with
        ActionExecutionRequest.Operations = operations }


let localRequest cache ops =
    cache
    |> buildCommand
    |> withOperations ops

let externalRequest cache fingerprint ops =
    cache
    |> buildCommand
    |> withFingerprint fingerprint
    |> withOperations ops
