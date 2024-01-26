namespace Terrabuild.Dotnet.Exec
open Terrabuild.Extensibility
open Helpers


type Arguments = {
    Command: string
    Arguments: string option
}

type Command() =
    interface ICommandFactory with
        member _.TypeOfArguments: System.Type option = Some typeof<Arguments>

        member _.GetSteps (parameters: obj): CommandLine list = 
            let parameters = parameters :?> Arguments

            let args = parameters.Arguments |> Option.defaultValue ""
            [ buildCmdLine parameters.Command args Cacheability.Always ]
