namespace SourceControls
open Environment

type Local() =
    interface Contracts.ISourceControl with
        override _.BranchOrTag = currentDir() |> Git.getBranchOrTag
        override _.IsTag = false
        override _.HeadCommit = currentDir() |> Git.getHeadCommit
        override _.CommitLog = currentDir() |> Git.getCommitLog
        override _.User = currentDir() |> Git.getCurrentUser
        override _.Run = None

        override _.LogType = Contracts.LogType.Terminal
        override _.LogError _ = ()
