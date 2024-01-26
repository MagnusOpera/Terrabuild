namespace Terrabuild.Terraform.Init
open Terrabuild.Extensibility
open Helpers

type Command() =
    interface ICommandFactory with
        member _.TypeOfArguments = None

        member _.GetSteps parameters =
            [ buildCmdLine "terraform" "init -reconfigure" ]
