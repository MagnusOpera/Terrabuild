namespace Terrabuild.Terraform.Workspace
open Terrabuild.Extensibility
open Helpers

type Arguments = {
    Workspace: string
}


type Command() =
    interface ICommandBuilder<Arguments> with
        member _.GetSteps parameters =
            [ buildCmdLine "terraform" "init -reconfigure"
              buildCmdLine "terraform" $"workspace select {parameters.Workspace}" ]
