namespace Extensions

open System
open Extensions

type Make(context) =
    inherit Extension(context)

    let getArgs (args: Map<string, string>) action =
        let args = args |> Seq.choose (fun kvp -> if kvp.Key.StartsWith("$") then Some (kvp.Key.Substring(1), kvp.Value)
                                                  else None)
        let arguments = args |> Seq.fold (fun acc (key, value) -> $"{acc} {key}=\"{value}\"") action
        arguments

    override _.Capabilities = Capabilities.Steps

    override _.Dependencies = NotSupportedException() |> raise

    override _.Outputs = NotSupportedException() |> raise

    override _.Ignores = NotSupportedException() |> raise

    override _.GetStep(action, args) =
        let arguments = getArgs args action
        [ { Command = "make"; Arguments = arguments } ]
