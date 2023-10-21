namespace SourceControls

type Local(workspaceDir: string) =
    inherit SourceControl(workspaceDir)

    override _.HeadCommit = "local"

    override _.BranchOrTag = "local"
