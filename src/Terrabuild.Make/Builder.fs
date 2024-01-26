namespace Terrabuild.Make
open Terrabuild.Extensibility


type Builder() =
    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    interface IBuilder with
        member _.Container = None

        member _.Dependencies = []

        member _.Outputs = []

        member _.Ignores = []

        member _.CreateCommand (action: string) =
            Target.Command(action)
