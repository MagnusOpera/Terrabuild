namespace Extensions
open System
open System.ComponentModel.Composition


type TerraformWorkspace = {
    Workspace: string
}

type TerraformPlan = {
    Workspace: string option
}

type TerraformApply = {
    Workspace: string option
}


[<Export(typeof<IExtension>)>]
type Terraform(context: IContext) =
    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    interface IExtension with
        override _.Container = Some "hashicorp/terraform:1.6"

        override _.Dependencies = [] 

        override _.Outputs = [ "terrabuild.planfile" ]

        override _.Ignores = [ ".terraform" ]

        override _.GetStepParameters action =
            match action with
            | "init" -> None
            | "workspace" -> Some typeof<TerraformWorkspace>
            | "plan" -> Some typeof<TerraformPlan>
            | "apply" -> Some typeof<TerraformApply>
            | _ -> ArgumentException($"Unknown action {action}") |> raise

        override _.BuildStepCommands (action, parameters) =
            match parameters, action with
            | _, "init" ->
                [ buildCmdLine "terraform" "init -reconfigure" ]
            | :? TerraformWorkspace as parameters, _ ->
                [ buildCmdLine "terraform" "init -reconfigure"
                  buildCmdLine "terraform" $"workspace select {parameters.Workspace}" ]
            | :? TerraformPlan as parameters, _ ->
                let workspace = parameters.Workspace
                [ buildCmdLine "terraform" "init -reconfigure"
                  if workspace |> Option.isSome then buildCmdLine "terraform" $"workspace select {workspace.Value}"
                  buildCmdLine "terraform" "plan -out=terrabuild.planfile" ]
            | :? TerraformApply as parameters, _ ->
                let workspace = parameters.Workspace
                [ buildCmdLine "terraform" "init -reconfigure"
                  if workspace |> Option.isSome then buildCmdLine "terraform" $"workspace select {workspace.Value}"
                  buildCmdLine "terraform"  "apply terrabuild.planfile" ]
            | _ -> ArgumentException($"Unknown action") |> raise
