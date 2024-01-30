namespace Terrabuild.Terraform.Init
open Terrabuild.Extensibility
open Helpers

type Command() =
    interface ICommandBuilder with
        member _.GetSteps () =
            [ buildCmdLine "terraform" "init -reconfigure" ]
