namespace Extensions
open System
open Extensions

type Docker(context) =
    inherit Extension(context)

    let dockerfile =
        if context.ProjectFile |> String.IsNullOrWhiteSpace then "Dockerfile"
        else context.ProjectFile

    let getArgs (args: Map<string, string>) =
        let args = args |> Seq.choose (fun kvp -> if kvp.Key.StartsWith("$") then Some (kvp.Key.Substring(1), kvp.Value)
                                                  else None)
        let arguments = args |> Seq.fold (fun acc (key, value) -> $"{acc} --build-arg {key}=\"{value}\"") ""
        arguments

    let getBuildStep (args: Map<string, string>) =
        let image = args["image"]
        let arguments = getArgs args
        let buildArgs = $"build --file {dockerfile} --tag {image}:$(terrabuild_node_hash) {arguments} ."

        if context.Shared then
            let pushArgs = $"push {image}:$(terrabuild_node_hash)"
            [ { Command = "docker"; Arguments = buildArgs}
              { Command = "docker"; Arguments = pushArgs} ]
        else
            [ { Command = "docker"; Arguments = buildArgs} ]

    let getPushStep (args: Map<string, string>) =
        let image = args["image"]
        let tag = args["tag"]

        if context.Shared then
            let retagArgs = $"buildx imagetools {image}:$(terrabuild_node_hash) {image}:{tag}"
            [ { Command = "docker"; Arguments = retagArgs} ]
        else
            let tagArgs = $"tag {image}:$(terrabuild_node_hash) {image}:{tag}"
            [ { Command = "docker"; Arguments = tagArgs} ]

    override _.Capabilities = Capabilities.Steps

    override _.Dependencies = NotSupportedException() |> raise

    override _.Outputs = NotSupportedException() |> raise

    override _.Ignores = NotSupportedException() |> raise

    override _.GetStep(action, args) =
        match action with
        | "build" -> getBuildStep args
        | "push" -> getPushStep args
        | _ -> failwith $"Unsupported action '{action}'"
