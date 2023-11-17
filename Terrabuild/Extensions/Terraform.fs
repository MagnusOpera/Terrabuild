namespace Extensions
open System


type TerraformWorkspace = {
    Workspace: string
}

type TerraformPlan = {
    Workspace: string
}

type TerraformApply = {
    Workspace: string
}


type Terraform(context) =
    inherit Extension(context)

    let buildCmdLine cmd args =
        { Extensions.CommandLine.Command = cmd
          Extensions.CommandLine.Arguments = args
          Extensions.CommandLine.Cache = Cacheability.Always }

    override _.Container = Some "hashicorp/terraform:1.6.4"

    override _.Dependencies = [] 

    override _.Outputs = [ "terrabuild.planfile" ]

    override _.Ignores = [ ".terraform" ]

    override _.GetStepParameters action =
        match action with
        | "init" -> null
        | "workspace" -> typeof<TerraformWorkspace>
        | "plan" -> typeof<TerraformPlan>
        | "apply" -> typeof<TerraformApply>
        | _ -> ArgumentException($"Unknown action {action}") |> raise

    override _.BuildStepCommands (action, parameters) =
        match parameters, action with
        | _, "init" ->
            [ buildCmdLine "terraform" "init -reconfigure" ]
        | :? TerraformWorkspace as parameters, _ ->
            [ buildCmdLine "terraform" "init -reconfigure"
              buildCmdLine "terraform" $"workspace select {parameters.Workspace}" ]
        | :? TerraformPlan as parameters, _ ->
            let workspace = parameters.Workspace |> Option.ofObj
            [ buildCmdLine "terraform" "init -reconfigure"
              if workspace |> Option.isSome then buildCmdLine "terraform" $"workspace select {workspace.Value}"
              buildCmdLine "terraform" "plan -out=terrabuild.planfile" ]
        | :? TerraformApply as parameters, _ ->
            let workspace = parameters.Workspace |> Option.ofObj
            [ buildCmdLine "terraform" "init -reconfigure"
              if workspace |> Option.isSome then buildCmdLine "terraform" $"workspace select {workspace.Value}"
              buildCmdLine "terraform"  "apply terrabuild.planfile" ]
        | _ -> ArgumentException($"Unknown action") |> raise
