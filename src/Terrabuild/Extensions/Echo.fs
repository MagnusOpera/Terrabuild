namespace Terrabuild.Echo
open Terrabuild.Extensibility
open System.ComponentModel.Composition


type Command(message: string) =
    let buildCmdLine cmd args =
        { Step.Command = cmd
          Step.Arguments = args
          Step.Cache = Cacheability.Always }

    interface ICommandBuilder with
        member _.GetSteps () = 
            [ buildCmdLine "echo" message ]


type Builder() =
    interface IBuilder with
        member _.Container = None

        member _.Dependencies = []

        member _.Outputs = []

        member _.Ignores = []

        member _.CreateCommand(action: string): ICommand = 
            Command(action)


[<Export("echo", typeof<IExtensionFactory>)>]
type EchoFactory() =
    interface IExtensionFactory with
        member _.CreateBuilder _ =
            Builder()
