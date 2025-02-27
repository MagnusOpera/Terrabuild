namespace Contracts

type LogType =
    | Terminal
    | Markdown of file:string

type RunInfo = {
    Name: string
    Repository: string
    RunId: string
    Message: string
    Author: string
    RunAttempt: int
}

type ISourceControl =
    abstract BranchOrTag: string
    abstract HeadCommit: string
    abstract User: string
    abstract Run: RunInfo option
    abstract LogType: LogType
    abstract LogError: string -> unit
