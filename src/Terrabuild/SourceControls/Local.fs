namespace SourceControls

type Local() =
    inherit Contracts.SourceControl()

    override _.HeadCommit =
        // NOTE: assuming current directory is a git repository
        System.Environment.CurrentDirectory |> Git.getHeadCommit

    override _.BranchOrTag =
        // NOTE: assuming current directory is a git repository
        System.Environment.CurrentDirectory |> Git.getBranchOrTag

    override _.LogType = Contracts.LogType.Terminal

    override _.LogError _ = ()

    override _.CI = false

    override _.Name = "Local"
wqdqdqw