namespace Terrabuild.Make.Target
open Terrabuild.Extensibility


type Arguments = {
    Parameters: Map<string, string>
}

type Command(action: string) =
    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    interface ICommandFactory<Arguments> with
        member _.GetSteps parameters =
            let args = parameters.Parameters |> Seq.fold (fun acc kvp -> $"{acc} {kvp.Key}=\"{kvp.Value}\"") $"{action}"
            [ buildCmdLine "make" args ]
