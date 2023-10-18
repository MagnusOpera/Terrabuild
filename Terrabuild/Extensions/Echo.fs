namespace Extensions
open System
open Extensions

type Echo(context) =
    inherit Extension(context)

    override _.Dependencies = []

    override _.Outputs = []

    override _.Ignores = []

    override _.GetStepParameters _ = null

    override _.BuildStepCommands (action, _) =
        [ { Command = "echo"; Arguments = action } ]
