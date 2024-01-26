namespace Terrabuild.Docker
open System
open Terrabuild.Extensibility


type Builder(context: Context) =
    interface IBuilder with
        member _.Container = None // Some "docker:24.0-dind"

        member _.Dependencies = []

        member _.Outputs = []

        member _.Ignores = []

        member _.CreateCommand (action: string) =
            match action with
            | "build" -> Build.Command(context)
            | "push" -> Push.Command(context)
            | _ -> ArgumentException($"Unknown action {action}") |> raise
