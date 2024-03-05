namespace Terrabuild.Extensions

open Terrabuild.Extensibility


  
type Terraform() =

    static member Init () =
        [ Action.Build "terraform" "init -reconfigure" Cacheability.Always ]


    static member Plan (workspace: string option) =
        [ Action.Build "terraform" "init -reconfigure" Cacheability.Always
          if workspace |> Option.isSome then Action.Build "terraform" $"workspace select {workspace.Value}" Cacheability.Always
          Action.Build "terraform" "plan -out=terrabuild.planfile" Cacheability.Always ]
  

    static member Apply (workspace: string option) =
        [ Action.Build "terraform" "init -reconfigure" Cacheability.Always
          if workspace |> Option.isSome then Action.Build "terraform" $"workspace select {workspace.Value}" Cacheability.Always
          Action.Build "terraform" "apply terrabuild.planfile" Cacheability.Always ]

