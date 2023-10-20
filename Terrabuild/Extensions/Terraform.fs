namespace Extensions
open System


type TerrformInit() =
    inherit StepParameters()

type TerrformWorkspace() =
    inherit StepParameters()
    member val Workspace = "default" with get, set

type TerraformPlan() =
    inherit StepParameters()
    member val Workspace: string = null with get, set

type TerraformApply() =
    inherit StepParameters()
    member val Workspace: string = null with get, set


type Terraform(context) =
    inherit Extension(context)

    override _.Dependencies = [] 

    override _.Outputs = [ "terrabuild.planfile" ]

    override _.Ignores = [ ".terraform" ]

    override _.GetStepParameters action =
        match action with
        | "init" -> typeof<TerrformInit>
        | "workspace" -> typeof<TerrformWorkspace>
        | "plan" -> typeof<TerraformPlan>
        | "apply" -> typeof<TerraformApply>
        | _ -> ArgumentException($"Unknown action {action}") |> raise

    override _.BuildStepCommands (_, parameters) =
        match parameters with
        | :? TerrformInit ->
            [ { Command = "terraform"; Arguments = $"init -reconfigure" } ]
        | :? TerrformWorkspace as parameters ->
            [ { Command = "terraform"; Arguments = $"init -reconfigure" }
              { Command = "terraform"; Arguments = $"workspace select {parameters.Workspace}" } ]
        | :? TerraformPlan as parameters ->
            let workspace = parameters.Workspace |> Option.ofObj
            [ { Command = "terraform"; Arguments = $"init -reconfigure" }
              if workspace |> Option.isSome then { Command = "terraform"; Arguments = $"workspace select {workspace.Value}" }
              { Command = "terraform"; Arguments = $"plan -out=terrabuild.planfile" } ]
        | :? TerraformApply as parameters ->
            let workspace = parameters.Workspace |> Option.ofObj
            [ { Command = "terraform"; Arguments = $"init -reconfigure" }
              if workspace |> Option.isSome then { Command = "terraform"; Arguments = $"workspace select {workspace.Value}" }
              { Command = "terraform"; Arguments = $"apply terrabuild.planfile" } ]
        | _ -> ArgumentException($"Unknown action") |> raise
