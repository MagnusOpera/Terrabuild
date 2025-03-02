module ConfigOptions
open System
open Collections
open Contracts

[<RequireQualifiedAccess>]
type Options = {
    Workspace: string
    WhatIf: bool
    Debug: bool
    MaxConcurrency: int
    Force: bool
    Retry: bool
    LocalOnly: bool
    StartedAt: DateTime
    Targets: string set
    Configuration: string
    LogType: Contracts.LogType
    Note: string option
    Tag: string option
    Labels: string set option
    Variables: Map<string, string>
    ContainerTool: string option

    // from SourceControl
    BranchOrTag: string
    HeadCommit: Commit
    CommitLog: Commit list
    Run: Contracts.RunInfo option
}
