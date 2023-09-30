namespace Extensions.Make

open System
open Extensions

type MakeExtension(context) =
    inherit Extension(context)

    override _.Capabilities = Capabilities.Steps

    override _.Dependencies = NotSupportedException() |> raise

    override _.Outputs = NotSupportedException() |> raise

    override _.Ignores = NotSupportedException() |> raise

    override _.GetStep(action, args) =
        let arguments = args |> Seq.fold (fun acc kvp -> $"{acc} {kvp.Key}=\"{kvp.Value}\"") $"{action}"
        [ { Command = "make"; Arguments = arguments } ]
