namespace Terrabuild.Npm.Install
open Terrabuild.Extensibility
open Helpers

type Command() =
    interface ICommandFactory with
        member _.TypeOfArguments = None

        member _.GetSteps parameters =
            [ buildCmdLine "npm" "ci" ]
