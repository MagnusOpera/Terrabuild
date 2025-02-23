namespace SourceControls
open Environment

type Local() =
    interface Contracts.ISourceControl with
        override _.BranchOrTag = currentDir() |> Git.getBranchOrTag
        override _.HeadCommit = currentDir() |> Git.getHeadCommit
        override _.User = currentDir() |> Git.getCurrentUser
        override _.Run = None

        override _.LogType = Contracts.LogType.Terminal
        override _.LogError _ = ()
