namespace Contracts

type LogType =
    | Terminal
    | Markdown of file:string

[<AbstractClass>]
type SourceControl() =
    abstract HeadCommit: string
    abstract BranchOrTag: string
    abstract LogType: LogType
    abstract CI: bool
    abstract Name: string

