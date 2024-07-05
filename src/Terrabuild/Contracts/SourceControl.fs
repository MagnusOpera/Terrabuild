namespace Contracts

type LogType =
    | Terminal
    | Markdown of file:string

type ISourceControl =
    abstract HeadCommit: string
    abstract BranchOrTag: string
    abstract LogType: LogType
    abstract LogError: string -> unit
    abstract CI: bool
    abstract Name: string
