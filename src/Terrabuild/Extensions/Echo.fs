namespace Extensions
open Extensions
open System.ComponentModel.Composition

type Echo() =
    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    interface IExtension with
        member _.Container = None

        member _.Dependencies = []

        member _.Outputs = []

        member _.Ignores = []

        member _.GetStepParameters _ = None

        member _.BuildStepCommands (action, _) =
            [ buildCmdLine "echo" action ]


[<Export("echo", typeof<IExtensionFactory>)>]
type EchoFactory() =
    interface IExtensionFactory with
        member _.Create _ =
            Echo()
