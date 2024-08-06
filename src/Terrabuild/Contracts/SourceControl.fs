namespace Contracts

type LogType =
    | Terminal
    | Markdown of file:string

type ISourceControl =
    abstract HeadCommit: string
    abstract BranchOrTag: string
    abstract LogType: LogType
    abstract LogError: string -> unit
    abstract CI: string option
    abstract Metadata: string option
