namespace Terrabuild.Npm.Test
open Terrabuild.Extensibility
open Helpers

type Command() =
    interface ICommandBuilder with
        member _.GetSteps () =
            [ buildCmdLine "npm" "ci"
              buildCmdLine "npm" "run test" ]
