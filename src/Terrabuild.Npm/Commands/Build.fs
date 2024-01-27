namespace Terrabuild.Npm.Build
open Terrabuild.Extensibility
open Helpers

type Command() =
    interface ICommandFactoryParameterless with
        member _.GetSteps () =
            [ buildCmdLine "npm" "ci"
              buildCmdLine "npm" "run build" ]
