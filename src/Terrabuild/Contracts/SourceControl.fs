namespace Contracts
open System

type LogType =
    | Terminal
    | Markdown of file:string

type Commit = {
    Sha: string
    Message: string
    Author: string
    Email: string
    Timestamp: DateTime
}

type RunInfo = {
    Name: string
    Repository: string
    OtherCommits: Commit list
    RunId: string
    IsTag: bool
    RunAttempt: int
}

type ISourceControl =
    abstract BranchOrTag: string
    abstract HeadCommit: Commit
    abstract CommitLog: Commit list
    abstract Run: RunInfo option
    abstract LogType: LogType
    abstract LogError: string -> unit
