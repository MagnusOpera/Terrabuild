namespace Terrabuild.Terraform.Apply
open Extensions
open Helpers

type Arguments = {
    Workspace: string option
}


type Command() =
    interface ICommandFactory with
        member _.TypeOfArguments = Some typeof<Arguments>

        member _.GetSteps parameters =
            let parameters = parameters :?> Arguments
            let workspace = parameters.Workspace

            [ buildCmdLine "terraform" "init -reconfigure"
              if workspace |> Option.isSome then buildCmdLine "terraform" $"workspace select {workspace.Value}"
              buildCmdLine "terraform" "apply terrabuild.planfile" ]
