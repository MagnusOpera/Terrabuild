namespace SourceControls

type Local() =
    inherit SourceControl()

    override _.HeadCommit = "local"

    override _.BranchOrTag = "local"

    override _.CI = false
