namespace Extensions
open System


type NpmInstall() =
    inherit StepParameters()


type NpmBuild() =
    inherit StepParameters()

type NpmTest() =
    inherit StepParameters()


type Npm(context) =
    inherit Extension(context)

    let buildCmdLine cmd args =
        { Extensions.CommandLine.Container = Some "node"
          Extensions.CommandLine.Command = cmd
          Extensions.CommandLine.Arguments = args }

    override _.Dependencies = [] 

    override _.Outputs = [ "dist" ]

    override _.Ignores = [ "node_modules" ]

    override _.GetStepParameters action =
        match action with
        | "install" -> typeof<NpmInstall>
        | "build" -> typeof<NpmBuild>
        | "test" -> typeof<NpmTest>
        | _ -> ArgumentException($"Unknown action {action}") |> raise

    override _.BuildStepCommands (_, parameters) =
        match parameters with
        | :? NpmInstall ->
            [ buildCmdLine "npm" "ci" ]
        | :? NpmBuild ->
            [ buildCmdLine "npm" "ci"
              buildCmdLine "npm" "run build" ]
        | :? NpmTest ->
            [ buildCmdLine "npm" "ci"
              buildCmdLine "npm" "run test" ]
        | _ -> ArgumentException($"Unknown action") |> raise
