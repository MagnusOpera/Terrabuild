module ConfigOptions
open System
open Collections

[<RequireQualifiedAccess>]
type Options = {
    Workspace: string
    WhatIf: bool
    Debug: bool
    MaxConcurrency: int
    Force: bool
    Retry: bool
    LocalOnly: bool
    CheckState: bool
    StartedAt: DateTime
    NoContainer: bool
    Targets: string set
    CI: string option
    Metadata: string option
    BranchOrTag: string
    HeadCommit: string
    Configuration: string
    LogType: Contracts.LogType
    Note: string option
    Tag: string option
    Labels: string set option
    Variables: Map<string, string>
}
