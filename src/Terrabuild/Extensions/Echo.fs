namespace Extensions
open Extensions

type Echo(context) =
    inherit Extension(context)

    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args }

    override _.Container = None

    override _.Dependencies = []

    override _.Outputs = []

    override _.Ignores = []

    override _.GetStepParameters _ = None

    override _.BuildStepCommands (action, _) =
        Cacheability.Always, [ buildCmdLine "echo" action ]
