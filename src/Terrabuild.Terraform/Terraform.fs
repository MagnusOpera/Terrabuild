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


type Terraform(context: IContext) =
    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    interface IExtension with
        member _.Container = Some "hashicorp/terraform:1.6"

        member _.Dependencies = [] 

        member _.Outputs = [ "terrabuild.planfile" ]

        member _.Ignores = [ ".terraform" ]

        member _.GetStepParameters action =
            match action with
            | "init" -> None
            | "workspace" -> Some typeof<TerraformWorkspace>
            | "plan" -> Some typeof<TerraformPlan>
            | "apply" -> Some typeof<TerraformApply>
            | _ -> ArgumentException($"Unknown action {action}") |> raise

        member _.BuildStepCommands (action, parameters) =
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


[<Export("terraform", typeof<IExtensionFactory>)>]
type TerraformFactory() =
    interface IExtensionFactory with
        member _.Create ctx =
            Terraform(ctx)
