namespace Extensions
open System
open Extensions

type Echo(context) =
    inherit Extension(context)

    override _.Capabilities = Capabilities.Steps

    override _.Dependencies = NotSupportedException() |> raise

    override _.Outputs = NotSupportedException() |> raise

    override _.Ignores = NotSupportedException() |> raise

    override _.GetStep(action, args) =
        [ { Command = "echo"; Arguments = action } ]
