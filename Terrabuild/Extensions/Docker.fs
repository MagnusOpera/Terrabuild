namespace Extensions.Docker
open System
open Extensions

type DockerExtension(workspaceDir, projectDir, projectFile, args) =
    inherit Extension(workspaceDir, projectDir, projectFile, args)

    let getBuildStep (args: Map<string, string>) =
        let arguments = args |> Seq.fold (fun acc kvp -> $"{acc} --build-arg {kvp.Key}={kvp.Value}") ""
        let buildArgs = $"build --file {projectFile} --tag terrabuild:$(terrabuild_node_hash) {arguments} ."
        [ { Command = "docker"; Arguments = buildArgs} ]

    let getPushStep (args: Map<string, string>) =
        let tag = args |> Map.find "tag"
        let tagArgs = $"tag terrabuild:$(terrabuild_node_hash) {tag}"        
        let pushArgs = $"push {tag}"

        [ { Command = "docker"; Arguments = tagArgs}
          { Command = "docker"; Arguments = pushArgs} ]

    override _.Capabilities = Capabilities.Steps

    override _.Dependencies = NotSupportedException() |> raise

    override _.Outputs = NotSupportedException() |> raise

    override _.Ignores = NotSupportedException() |> raise


    override _.GetStep(action, args) =
        match action with
        | "build" -> getBuildStep args
        | "push" -> getPushStep args
        | _ -> failwith $"Unsupported action '{action}'"
