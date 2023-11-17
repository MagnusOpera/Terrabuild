namespace Extensions
open Extensions

type Echo(context) =
    inherit Extension(context)

    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    override _.Container = None

    override _.Dependencies = []

    override _.Outputs = []

    override _.Ignores = []

    override _.GetStepParameters _ = None

    override _.BuildStepCommands (action, _) =
        [ buildCmdLine "echo" action ]
