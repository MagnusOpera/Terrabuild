namespace Extensions.Shell
open System
open Extensions

type ShellExtension(workspaceDir, projectDir, projectFile, args) =
    inherit Extension(workspaceDir, projectDir, projectFile, args)

    override _.Capabilities = Capabilities.Steps

    override _.Dependencies = NotSupportedException() |> raise

    override _.Outputs = NotSupportedException() |> raise

    override _.Ignores = NotSupportedException() |> raise

    override _.GetStep(action, args) =
        let arguments = args |> Map.tryFind "args" |> Option.defaultValue ""
        [ { Command = action; Arguments = arguments } ]