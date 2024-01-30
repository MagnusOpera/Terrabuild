namespace Terrabuild.Make.Target
open Terrabuild.Extensibility


type Arguments = {
    Parameters: Map<string, string>
}

type Command(action: string) =
    let buildCmdLine cmd args =
        { Step.Command = cmd
          Step.Arguments = args
          Step.Cache = Cacheability.Always }

    interface ICommandBuilder<Arguments> with
        member _.GetSteps parameters =
            let args = parameters.Parameters |> Seq.fold (fun acc kvp -> $"{acc} {kvp.Key}=\"{kvp.Value}\"") $"{action}"
            [ buildCmdLine "make" args ]
