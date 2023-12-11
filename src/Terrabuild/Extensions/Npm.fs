namespace Extensions
open System


type Npm(context) =
    inherit Extension(context)

    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args }

    override _.Container = Some "node:20.9"

    override _.Dependencies = [] 

    override _.Outputs = [ "dist" ]

    override _.Ignores = [ "node_modules" ]

    override _.GetStepParameters action =
        match action with
        | "install" -> None
        | "build" -> None
        | "test" -> None
        | _ -> ArgumentException($"Unknown action {action}") |> raise

    override _.BuildStepCommands (action, parameters) =
        match parameters, action with
        | _, "install" ->
            Cacheability.Always, [ buildCmdLine "npm" "ci" ]
        | _, "build" ->
            Cacheability.Always, [ buildCmdLine "npm" "ci"
                                   buildCmdLine "npm" "run build" ]
        | _, "test" ->
            Cacheability.Always, [ buildCmdLine "npm" "ci"
                                   buildCmdLine "npm" "run test" ]
        | _ -> ArgumentException($"Unknown action") |> raise
