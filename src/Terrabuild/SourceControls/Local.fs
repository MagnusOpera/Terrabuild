namespace SourceControls

type Local() =
    inherit Contracts.SourceControl()

    override _.HeadCommit =
        // NOTE: assuming current directory is a git repository
        System.Environment.CurrentDirectory |> Git.getHeadCommit

    override _.BranchOrTag =
        // NOTE: assuming current directory is a git repository
        System.Environment.CurrentDirectory |> Git.getBranchOrTag

    override _.CI = false

    override _.Name = "Local"

    override _.Log success title =
        let color = 
            if success then $"{Ansi.Styles.green}{Ansi.Emojis.checkmark}"
            else $"{Ansi.Styles.red}{Ansi.Emojis.crossmark}"

        $"{color} {title}{Ansi.Styles.reset}", ""
