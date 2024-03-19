namespace Terrabuild.Extensions

open Terrabuild.Extensibility


/// `terraform` extension provides commands to handle a Terraform project.
type Terraform() =

    /// <summary>
    /// Provides default values for project.
    /// </summary>
    /// <param name="ignores" example="[ &quot;.terraform/&quot; &quot;*.tfstate/&quot; ]">Default values.</param>
    /// <param name="outputs" example="[ &quot;*.planfile&quot; ]">Default values.</param>
    static member __init__() =
        let projectInfo = 
            { ProjectInfo.Default
              with Ignores = Set [ ".terraform/"; "*.tfstate/" ]
                   Outputs = Set [ "*.planfile" ] }
        projectInfo

    /// <summary weight="1">
    /// Init Terraform.
    /// </summary>
    static member init () =
        scope Cacheability.Always
        |> andThen "terraform" "init -reconfigure"


    /// <summary weight="2" title="Generate plan file.">
    /// This command generates the planfile:
    /// * initialize Terraform
    /// * select workspace
    /// * run plan
    /// </summary>
    /// <param name="workspace" example="&quot;dev&quot;">Workspace to use. Use `default` if not provided.</param>
    static member plan (workspace: string option) =
        scope Cacheability.Always
        |> andThen "terraform" "init -reconfigure"
        |> andIf (workspace |> Option.isSome) (fun batch -> batch |> andThen "terraform" $"workspace select {workspace.Value}")
        |> andThen "terraform" "plan -out=terrabuild.planfile"
  

    /// <summary weight="3" title="Apply plan file.">
    /// Apply the plan file:
    /// * initialize Terraform
    /// * select workspace
    /// * apply plan
    /// </summary>
    /// <param name="workspace" example="&quot;dev&quot;">Workspace to use. Use `default` if not provided.</param>
    static member apply (workspace: string option) =
        scope Cacheability.Always
        |> andThen "terraform" "init -reconfigure" 
        |> andIf (workspace |> Option.isSome) (fun batch -> batch |> andThen "terraform" $"workspace select {workspace.Value}")
        |> andThen "terraform" "apply terrabuild.planfile"
