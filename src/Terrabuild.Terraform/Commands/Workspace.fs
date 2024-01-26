namespace Terrabuild.Terraform.Workspace
open Terrabuild.Extensibility
open Helpers

type Arguments = {
    Workspace: string
}


type Command() =
    interface ICommandFactory with
        member _.TypeOfArguments = Some typeof<Arguments>

        member _.GetSteps parameters =
            let parameters = parameters :?> Arguments

            [ buildCmdLine "terraform" "init -reconfigure"
              buildCmdLine "terraform" $"workspace select {parameters.Workspace}" ]
