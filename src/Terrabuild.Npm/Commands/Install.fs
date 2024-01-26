namespace Terrabuild.Npm.Install
open Extensions
open Helpers

type Command() =
    interface ICommandFactory with
        member _.TypeOfArguments = None

        member _.GetSteps parameters =
            [ buildCmdLine "npm" "ci" ]
