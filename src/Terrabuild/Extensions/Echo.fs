namespace Terrabuild.Echo
open Terrabuild.Extensibility
open System.ComponentModel.Composition


type Command(message: string) =
    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    interface ICommandFactoryParameterless with
        member _.GetSteps () = 
            [ buildCmdLine "echo" message ]


type Builder() =
    interface IBuilder with
        member _.Container = None

        member _.Dependencies = []

        member _.Outputs = []

        member _.Ignores = []

        member _.CreateCommand(action: string): ICommandFactory = 
            Command(action)


[<Export("echo", typeof<IExtensionFactory>)>]
type EchoFactory() =
    interface IExtensionFactory with
        member _.CreateBuilder _ =
            Builder()
