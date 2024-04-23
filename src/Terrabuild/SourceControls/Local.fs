namespace SourceControls

type Local() =
    inherit Contracts.SourceControl()

    override _.HeadCommit = "local"

    override _.BranchOrTag = "local"

    override _.CI = false

    override _.Name = "Local"
