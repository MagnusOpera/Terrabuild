namespace Terrabuild.Npm
open System
open Terrabuild.Extensibility


type Builder() =

    interface IBuilder with
        member _.Container = Some "node:20.9"

        member _.Dependencies = [] 

        member _.Outputs = [ "dist" ]

        member _.Ignores = [ "node_modules" ]

        member _.CreateCommand (action: string) =
            match action with
            | "install" -> Install.Command()
            | "build" -> Build.Command()
            | "test" -> Test.Command()
            | _ -> ArgumentException($"Unknown action {action}") |> raise
