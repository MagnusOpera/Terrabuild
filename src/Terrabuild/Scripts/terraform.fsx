#if !TERRABUILD_SCRIPT
#r "../bin/Debug/net8.0/Terrabuild.Extensibility.dll"
#endif

open Terrabuild.Extensibility

type Globals = {
    Workspace: string option
}

let mutable globals = None


let buildCmdLine cmd args =
    { Action.Command = cmd
      Action.Arguments = args
      Action.Cache = Cacheability.Always }

let init (workspace: string option) =
    globals <- Some { Workspace = workspace }

    [ buildCmdLine "terraform" "init -reconfigure" ]

let plan () =
    let globals = globals.Value

    [ buildCmdLine "terraform" "init -reconfigure"
      if globals.Workspace |> Option.isSome then buildCmdLine "terraform" $"workspace select {globals.Workspace.Value}"
      buildCmdLine "terraform" "plan -out=terrabuild.planfile" ]

let apply () =
    let globals = globals.Value

    [ buildCmdLine "terraform" "init -reconfigure"
      if globals.Workspace |> Option.isSome then buildCmdLine "terraform" $"workspace select {globals.Workspace.Value}"
      buildCmdLine "terraform" "apply terrabuild.planfile" ]
