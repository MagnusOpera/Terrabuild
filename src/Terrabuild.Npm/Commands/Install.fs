namespace Terrabuild.Npm.Install
open Terrabuild.Extensibility
open Helpers

type Command() =
    interface ICommandBuilder with
        member _.GetSteps () =
            [ buildCmdLine "npm" "ci" ]
