namespace Extensions
open Extensions

type Echo(context) =
    inherit Extension(context)

    let buildCmdLine cmd args =
        { Extensions.CommandLine.Command = cmd
          Extensions.CommandLine.Arguments = args
          Extensions.CommandLine.Cache = Cacheability.Always }

    override _.Container = None

    override _.Dependencies = []

    override _.Outputs = []

    override _.Ignores = []

    override _.GetStepParameters _ = None

    override _.BuildStepCommands (action, _) =
        [ buildCmdLine "echo" action ]
