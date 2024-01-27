namespace Terrabuild.Dotnet.Exec
open Terrabuild.Extensibility
open Helpers


type Arguments = {
    Command: string
    Arguments: string option
}

type Command() =
    interface ICommandFactory<Arguments> with
        member _.GetSteps parameters = 
            let args = parameters.Arguments |> Option.defaultValue ""
            [ buildCmdLine parameters.Command args Cacheability.Always ]
