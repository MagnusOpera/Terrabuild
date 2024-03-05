namespace Terrabuild.Extensions

open Terrabuild.Extensibility


  
type Terraform() =
    static member init () =
        [ Action.Build "terraform" "init -reconfigure" Cacheability.Always ]

    static member plan (workspace: string option) =
        [ Action.Build "terraform" "init -reconfigure" Cacheability.Always
          if workspace |> Option.isSome then Action.Build "terraform" $"workspace select {workspace.Value}" Cacheability.Always
          Action.Build "terraform" "plan -out=terrabuild.planfile" Cacheability.Always ]
  
    static member apply (workspace: string option) =
        [ Action.Build "terraform" "init -reconfigure" Cacheability.Always
          if workspace |> Option.isSome then Action.Build "terraform" $"workspace select {workspace.Value}" Cacheability.Always
          Action.Build "terraform" "apply terrabuild.planfile" Cacheability.Always ]
