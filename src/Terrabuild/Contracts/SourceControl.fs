namespace Contracts

type LogType =
    | Terminal
    | Markdown of file:string

type RunInfo = {
    Name: string
    Repository: string
    ParentCommits: string list
    RunId: string
    IsTag: bool
    RunAttempt: int
}

type Commit = {
    Sha: string
    Subject: string
    Author: string
    Email: string
}

type ISourceControl =
    abstract BranchOrTag: string
    abstract HeadCommit: Commit
    abstract CommitLog: Commit list
    abstract Run: RunInfo option
    abstract LogType: LogType
    abstract LogError: string -> unit
