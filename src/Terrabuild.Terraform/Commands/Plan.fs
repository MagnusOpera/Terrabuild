namespace Terrabuild.Terraform.Plan
open Terrabuild.Extensibility
open Helpers


type Arguments = {
    Workspace: string option
}


type Command() =
    interface ICommandBuilder<Arguments> with
        member _.GetSteps parameters =
            let workspace = parameters.Workspace
            [ buildCmdLine "terraform" "init -reconfigure"
              if workspace |> Option.isSome then buildCmdLine "terraform" $"workspace select {workspace.Value}"
              buildCmdLine "terraform" "plan -out=terrabuild.planfile" ]
