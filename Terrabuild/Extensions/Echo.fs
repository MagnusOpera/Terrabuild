namespace Extensions
open Extensions

type Echo(context) =
    inherit Extension(context)

    let buildCmdLine cmd args =
        { Extensions.CommandLine.Container = None
          Extensions.CommandLine.ContainerTag = None
          Extensions.CommandLine.Command = cmd
          Extensions.CommandLine.Arguments = args }

    override _.Dependencies = []

    override _.Outputs = []

    override _.Ignores = []

    override _.GetStepParameters _ = null

    override _.BuildStepCommands (action, _) =
        [ buildCmdLine "echo" action ]
