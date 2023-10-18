namespace Extensions

open System
open Extensions

type MakeCommand() =
    inherit StepParameters()
    member val Parameters = System.Collections.Generic.Dictionary<string, string>() with get, set


type Make(context) =
    inherit Extension(context)

    override _.Dependencies = []

    override _.Outputs = []

    override _.Ignores = []

    override _.GetStepParameters _ = typeof<MakeCommand>

    override _.BuildStepCommands (action, parameters) =
        match parameters with
        | :? MakeCommand as parameters ->
            let args = parameters.Parameters |> Seq.fold (fun acc kvp -> $"{acc} {kvp.Key}=\"{kvp.Value}\"") $"{action}"
            [ { Command = "make"; Arguments = args } ]
        | _ -> ArgumentException($"Unknown action {action}") |> raise
