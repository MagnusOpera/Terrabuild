namespace Extensions
open Extensions

type Echo(context: IContext) =
    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    interface IExtension with
        member _.Container = None

        member _.Dependencies = []

        member _.Outputs = []

        member _.Ignores = []

        member _.GetStepParameters _ = None

        member _.BuildStepCommands (action, _) =
            [ buildCmdLine "echo" action ]
