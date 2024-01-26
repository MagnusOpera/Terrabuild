namespace Terrabuild.Npm.Build
open Terrabuild.Extensibility
open Helpers

type Command() =
    interface ICommandFactory with
        member _.TypeOfArguments = None

        member _.GetSteps parameters =
            [ buildCmdLine "npm" "ci"
              buildCmdLine "npm" "run build" ]
