namespace SourceControls

[<AbstractClass>]
type SourceControl(workspaceDir:string) =
    abstract HeadCommit: string
    abstract BranchOrTag: string
