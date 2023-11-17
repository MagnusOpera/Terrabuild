namespace Extensions

open System
open Extensions

type MakeCommand = {
    Parameters: Map<string, string>
}


type Make(context) =
    inherit Extension(context)

    let buildCmdLine cmd args =
        { Extensions.CommandLine.Command = cmd
          Extensions.CommandLine.Arguments = args
          Extensions.CommandLine.Cache = Cacheability.Always }

    override _.Container = None

    override _.Dependencies = []

    override _.Outputs = []

    override _.Ignores = []

    override _.GetStepParameters _ = Some typeof<MakeCommand>

    override _.BuildStepCommands (action, parameters) =
        match parameters, action with
        | :? MakeCommand as parameters, _ ->
            let args = parameters.Parameters |> Seq.fold (fun acc kvp -> $"{acc} {kvp.Key}=\"{kvp.Value}\"") $"{action}"
            [ buildCmdLine "make" args ]
        | _ -> ArgumentException($"Unknown action {action}") |> raise
