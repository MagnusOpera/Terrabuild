namespace Terrabuild.Npm.Install
open Terrabuild.Extensibility
open Helpers

type Command() =
    interface ICommandFactoryParameterless with
        member _.GetSteps () =
            [ buildCmdLine "npm" "ci" ]
