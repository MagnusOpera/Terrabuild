namespace Extensions
open Extensions
open System

type ShellCommand = {
    Arguments: string option
}

type Shell(context) =
    inherit Extension(context)

    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args }

    override _.Container = None

    override _.Dependencies = []

    override _.Outputs = []

    override _.Ignores = []

    override _.GetStepParameters _ = Some typeof<ShellCommand>

    override _.BuildStepCommands (action, parameters) =
        match parameters, action with
        | :? ShellCommand as parameters, _ ->
            let args = parameters.Arguments |> Option.defaultValue ""
            Cacheability.Always, [ buildCmdLine action args ]
        | _ -> ArgumentException($"Unknown action {action}") |> raise
