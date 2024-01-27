namespace Extensions.Shell
open Terrabuild.Extensibility
open System.ComponentModel.Composition

type Arguments = {
    Arguments: string option
}


type Command(action: string) =
    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    interface ICommandFactory<Arguments> with
        member _.GetSteps parameters = 
            let args = parameters.Arguments |> Option.defaultValue ""
            [ buildCmdLine action args ]


type Builder() =
    interface IBuilder with
        member _.Container = None

        member _.Dependencies = []

        member _.Outputs = []

        member _.Ignores = []

        member _.CreateCommand(action: string): ICommandFactory = 
            Command(action)


[<Export("shell", typeof<IExtensionFactory>)>]
type ShellFactory() =
    interface IExtensionFactory with
        member _.CreateBuilder _ =
            Builder()
