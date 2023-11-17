namespace Extensions
open Extensions
open System

type ShellCommand = {
    Arguments: string
}

type Shell(context) =
    inherit Extension(context)

    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    override _.Container = None

    override _.Dependencies = []

    override _.Outputs = []

    override _.Ignores = []

    override _.GetStepParameters _ = Some typeof<ShellCommand>

    override _.BuildStepCommands (action, parameters) =
        match parameters, action with
        | :? ShellCommand as parameters, _ ->
            [ buildCmdLine action parameters.Arguments ]
        | _ -> ArgumentException($"Unknown action {action}") |> raise
