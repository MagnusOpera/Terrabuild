namespace Extensions
open System
open Extensions

type Shell(context) =
    inherit Extension(context)

    override _.Capabilities = Capabilities.Steps

    override _.Dependencies = NotSupportedException() |> raise

    override _.Outputs = NotSupportedException() |> raise

    override _.Ignores = NotSupportedException() |> raise

    override _.GetStep(action, args) =
        let arguments = args |> Map.tryFind "arguments" |> Option.defaultValue ""
        [ { Command = action; Arguments = arguments } ]
