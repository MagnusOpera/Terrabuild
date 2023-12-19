namespace Extensions

open System
open Extensions

type MakeCommand = {
    Parameters: Map<string, string>
}


type Make(context: IContext) =
    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    interface IExtension with
        member _.Container = None

        member _.Dependencies = []

        member _.Outputs = []

        member _.Ignores = []

        member _.GetStepParameters _ = Some typeof<MakeCommand>

        member _.BuildStepCommands (action, parameters) =
            match parameters, action with
            | :? MakeCommand as parameters, _ ->
                let args = parameters.Parameters |> Seq.fold (fun acc kvp -> $"{acc} {kvp.Key}=\"{kvp.Value}\"") $"{action}"
                [ buildCmdLine "make" args ]
            | _ -> ArgumentException($"Unknown action {action}") |> raise
