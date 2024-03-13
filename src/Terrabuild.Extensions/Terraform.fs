namespace Terrabuild.Extensions

open Terrabuild.Extensibility


  
type Terraform() =

    static member __init__() =
        let projectInfo = { ProjectInfo.Properties = Map.empty
                            ProjectInfo.Ignores = Set [ ".terraform/"; "*.tfstate/" ]
                            ProjectInfo.Outputs = Set [ "*.planfile" ]
                            ProjectInfo.Dependencies = Set.empty }
        projectInfo


    static member init () =
        scope Cacheability.Always
        |> andThen "terraform" "init -reconfigure"


    static member plan (workspace: string option) =
        scope Cacheability.Always
        |> andThen "terraform" "init -reconfigure"
        |> andIf (workspace |> Option.isSome) (fun batch -> batch |> andThen "terraform" $"workspace select {workspace.Value}")
        |> andThen "terraform" "plan -out=terrabuild.planfile"
  

    static member apply (workspace: string option) =
        scope Cacheability.Always
        |> andThen "terraform" "init -reconfigure" 
        |> andIf (workspace |> Option.isSome) (fun batch -> batch |> andThen "terraform" $"workspace select {workspace.Value}")
        |> andThen "terraform" "apply terrabuild.planfile"
