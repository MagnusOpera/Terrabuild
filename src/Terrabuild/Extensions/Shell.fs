namespace Extensions
open Extensions
open System
open System.ComponentModel.Composition

type ShellCommand = {
    Arguments: string option
}

type Shell() =
    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    interface IExtension with
        member _.Container = None

        member _.Dependencies = []

        member _.Outputs = []

        member _.Ignores = []

        member _.GetStepParameters _ = Some typeof<ShellCommand>

        member _.BuildStepCommands (action, parameters) =
            match parameters, action with
            | :? ShellCommand as parameters, _ ->
                let args = parameters.Arguments |> Option.defaultValue ""
                [ buildCmdLine action args ]
            | _ -> ArgumentException($"Unknown action {action}") |> raise


[<Export("shell", typeof<IExtensionFactory>)>]
type ShellFactory() =
    interface IExtensionFactory with
        member _.Create _ =
            Shell()
