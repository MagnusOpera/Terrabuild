namespace Terrabuild.Terraform
open Terrabuild.Extensibility
open System


type Builder() =
    interface IBuilder with
        member _.Container = Some "hashicorp/terraform:1.6"

        member _.Dependencies = [] 

        member _.Outputs = [ "terrabuild.planfile" ]

        member _.Ignores = [ ".terraform" ]

        member _.CreateCommand(action: string): ICommand = 
            match action with
            | "init" -> Init.Command()
            | "workspace" -> Workspace.Command()
            | "plan" -> Plan.Command()
            | "apply" -> Apply.Command()
            | _ -> ArgumentException($"Unknown action {action}") |> raise
