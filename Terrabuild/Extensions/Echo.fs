namespace Extensions.Echo
open System
open Extensions

type EchoExtension(workspaceDir, projectDir, projectFile, args) =
    inherit Extension(workspaceDir, projectDir, projectFile, args)

    override _.Capabilities = Capabilities.Steps

    override _.Dependencies = NotSupportedException() |> raise

    override _.Outputs = NotSupportedException() |> raise

    override _.Ignores = NotSupportedException() |> raise

    override _.GetStep(action, args) =
        [ { Command = "echo"; Arguments = action } ]
