namespace Terrabuild.Extensions

open Terrabuild.Extensibility


/// `terraform` extension provides commands to handle a Terraform project.
type Terraform() =

    /// <summary>
    /// Provides default values for project.
    /// </summary>
    /// <param name="ignores" example="[ &quot;.terraform/&quot; &quot;*.tfstate/&quot; ]">Default values.</param>
    /// <param name="outputs" example="[ &quot;*.planfile&quot; ]">Default values.</param>
    static member __defaults__() =
        let projectInfo = 
            { ProjectInfo.Default
              with Ignores = Set [ ".terraform/"; "*.tfstate/" ]
                   Outputs = Set [ "*.planfile" ] }
        projectInfo

    /// <summary weight="1">
    /// Init Terraform.
    /// </summary>
    static member init (context: ActionContext) =
        let ops = All [ shellOp "terraform" "init" ]
        execRequest Cacheability.Always [] ops


    /// <summary weight="2" title="Generate plan file.">
    /// This command validates the project:
    /// * initialize Terraform
    /// * select workspace
    /// * run validate
    /// </summary>
    /// <param name="workspace" example="&quot;dev&quot;">Workspace to use. Use `default` if not provided.</param>
    /// <param name="variables" example="{ configuration: &quot;Release&quot; }">Variables for plan (see Terraform [Variables](https://developer.hashicorp.com/terraform/language/values/variables#variables-on-the-command-line)).</param> 
    static member validate (context: ActionContext) (workspace: string option) =
        let ops = All [
            shellOp "terraform" "init"
            
            match workspace with
            | Some workspace -> shellOp "terraform" $"workspace select {workspace}"
            | _ -> ()

            shellOp "terraform" "validate"
        ]
        execRequest Cacheability.Always [] ops


    /// <summary weight="3" title="Generate plan file.">
    /// This command generates the planfile:
    /// * initialize Terraform
    /// * select workspace
    /// * run plan
    /// </summary>
    /// <param name="workspace" example="&quot;dev&quot;">Workspace to use. Use `default` if not provided.</param>
    /// <param name="variables" example="{ configuration: &quot;Release&quot; }">Variables for plan (see Terraform [Variables](https://developer.hashicorp.com/terraform/language/values/variables#variables-on-the-command-line)).</param> 
    static member plan (context: ActionContext) (workspace: string option) (variables: Map<string, string>) =
        let vars = variables |> Seq.fold (fun acc (KeyValue(key, value)) -> acc + $" -var=\"{key}={value}\"") ""

        let ops = All [
            shellOp "terraform" "init"
            
            match workspace with
            | Some workspace -> shellOp "terraform" $"workspace select {workspace}"
            | _ -> ()

            shellOp "terraform" $"plan -out=terrabuild.planfile{vars}"
        ]
        execRequest Cacheability.Always [] ops
  

    /// <summary weight="4" title="Apply plan file.">
    /// Apply the plan file:
    /// * initialize Terraform
    /// * select workspace
    /// * apply plan
    /// </summary>
    /// <param name="workspace" example="&quot;dev&quot;">Workspace to use. Use `default` if not provided.</param>
    static member apply (context: ActionContext) (workspace: string option) =
        let ops = All [
            shellOp "terraform" "init"
            
            match workspace with
            | Some workspace -> shellOp "terraform" $"workspace select {workspace}"
            | _ -> ()

            shellOp "terraform" "apply terrabuild.planfile"
        ]
        execRequest Cacheability.Always [] ops
  