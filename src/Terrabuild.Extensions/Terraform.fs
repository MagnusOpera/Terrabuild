namespace Terrabuild.Extensions

open Terrabuild.Extensibility


  
type Terraform() =

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
