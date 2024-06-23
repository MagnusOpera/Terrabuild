namespace Contracts

type LogType =
    | Terminal
    | Markdown of file:string

[<AbstractClass>]
type SourceControl() =
    abstract HeadCommit: string
    abstract BranchOrTag: string
    abstract CI: bool
    abstract Name: string
    abstract LogType: unit -> LogType

