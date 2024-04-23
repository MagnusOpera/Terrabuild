namespace Contracts

[<AbstractClass>]
type SourceControl() =
    abstract HeadCommit: string
    abstract BranchOrTag: string
    abstract CI: bool
    abstract Name: string
