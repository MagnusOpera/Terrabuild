namespace Extensions
open System
open Extensions

type ShellCommand() =
    inherit StepParameters()
    member val Arguments = "" with get, set

type Shell(context) =
    inherit Extension(context)

    override _.Dependencies = []

    override _.Outputs = []

    override _.Ignores = []

    override _.GetStepParameters _ = typeof<ShellCommand>

    override _.BuildStepCommands (action, parameters) =
        match parameters with
        | :? ShellCommand as parameters ->
            [ { Command = action; Arguments = parameters.Arguments } ]
        | _ -> ArgumentException($"Unknown action {action}") |> raise
