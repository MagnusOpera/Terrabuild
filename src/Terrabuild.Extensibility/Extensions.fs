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

[<RequireQualifiedAccess>]
type Operation =
    { Fingerprint: ShellOperation option
      Core: ShellOperation }

[<Flags>]
type Cacheability =
    | Never = 0
    | Local = 1
    | Remote = 2
    | Always = 3 // Local + Remote

[<RequireQualifiedAccess>]
type ActionExecutionRequest =
    { Cache: Cacheability
      Operations: Operation list }


let shellOp cmd args =
    { ShellOperation.Command = cmd
      ShellOperation.Arguments = args }

let localOp cmd args = 
    { Operation.Fingerprint = None
      Operation.Core = shellOp cmd args }

let externalOp fingerprint cmd args =
    { Operation.Fingerprint = Some fingerprint
      Operation.Core = shellOp cmd args }

let execRequest cache ops =
    { ActionExecutionRequest.Cache = cache 
      ActionExecutionRequest.Operations = ops }
