namespace Terrabuild.Terraform.Init
open Terrabuild.Extensibility
open Helpers

type Command() =
    interface ICommandFactoryParameterless with
        member _.GetSteps () =
            [ buildCmdLine "terraform" "init -reconfigure" ]
