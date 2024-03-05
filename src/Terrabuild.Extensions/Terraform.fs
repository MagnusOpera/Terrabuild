module Terraform
type Dummy = interface end

open Terrabuild.Extensibility


let buildCmdLine cmd args =
    { Action.Command = cmd
      Action.Arguments = args
      Action.Cache = Cacheability.Always }

let init (workspace: string option) =
    [ buildCmdLine "terraform" "init -reconfigure" ]

let plan (workspace: string option) =
    [ buildCmdLine "terraform" "init -reconfigure"
      if workspace |> Option.isSome then buildCmdLine "terraform" $"workspace select {workspace.Value}"
      buildCmdLine "terraform" "plan -out=terrabuild.planfile" ]

let apply (workspace: string option) =
    [ buildCmdLine "terraform" "init -reconfigure"
      if workspace |> Option.isSome then buildCmdLine "terraform" $"workspace select {workspace.Value}"
      buildCmdLine "terraform" "apply terrabuild.planfile" ]
