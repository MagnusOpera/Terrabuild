namespace SourceControls

type Local() =
    interface Contracts.ISourceControl with
        override _.BranchOrTag = System.Environment.CurrentDirectory |> Git.getBranchOrTag
        override _.HeadCommit = System.Environment.CurrentDirectory |> Git.getHeadCommit
        override _.User = System.Environment.CurrentDirectory |> Git.getCurrentUser
        override _.Run = None

        override _.LogType = Contracts.LogType.Terminal
        override _.LogError _ = ()
