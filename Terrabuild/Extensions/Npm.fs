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
            [ { Command = "npm"; Arguments = $"ci" } ]
        | :? NpmBuild ->
            [ { Command = "npm"; Arguments = $"ci" }
              { Command = "npm"; Arguments = $"run build" } ]
        | :? NpmTest ->
            [ { Command = "npm"; Arguments = $"ci" }
              { Command = "npm"; Arguments = $"run test" } ]
        | _ -> ArgumentException($"Unknown action") |> raise
