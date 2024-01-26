namespace Extensions.Shell
open Extensions
open System.ComponentModel.Composition

type Arguments = {
    Arguments: string option
}


type Command(action: string) =
    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    interface ICommandFactory with
        member _.TypeOfArguments: System.Type option = Some typeof<Arguments>

        member _.GetSteps (parameters: obj): CommandLine list = 
            let parameters = parameters :?> Arguments

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
