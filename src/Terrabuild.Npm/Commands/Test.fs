namespace Terrabuild.Npm.Test
open Terrabuild.Extensibility
open Helpers

type Command() =
    interface ICommandFactoryParameterless with
        member _.GetSteps () =
            [ buildCmdLine "npm" "ci"
              buildCmdLine "npm" "run test" ]
