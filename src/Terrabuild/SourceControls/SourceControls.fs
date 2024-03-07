namespace SourceControls

[<AbstractClass>]
type SourceControl() =
    abstract HeadCommit: string
    abstract BranchOrTag: string
    abstract CI: bool
