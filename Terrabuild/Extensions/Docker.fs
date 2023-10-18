namespace Extensions
open System
open Extensions

type DockerBuild() =
    inherit StepParameters()
    member val Image = "" with get, set
    member val Arguments = System.Collections.Generic.Dictionary<string, string>() with get, set

type DockerPush() =
    inherit StepParameters()
    member val Image = "" with get, set
    member val Tag = "" with get, set

type Docker(context) =
    inherit Extension(context)

    let dockerfile =
        match context.With with
        | Some dockerfile -> dockerfile
        | _ -> "Dockerfile"

    override _.Dependencies = []

    override _.Outputs = []

    override _.Ignores = []

    override _.GetStepParameters action =
        match action with
        | "build" -> typeof<DockerBuild>
        | "push" -> typeof<DockerPush>
        | _ -> ArgumentException($"Unknown action {action}") |> raise

    override _.BuildStepCommands (_, parameters) =
        match parameters with
        | :? DockerBuild as parameters ->
            let args = parameters.Arguments |> Seq.fold (fun acc kvp -> $"{acc} --build-arg {kvp.Key}=\"{kvp.Value}\"") ""
            let buildArgs = $"build --file {dockerfile} --tag {parameters.Image}:{parameters.NodeHash} {args} ."

            if parameters.Shared then
                let pushArgs = $"push {parameters.Image}:{parameters.NodeHash}"
                [ { Command = "docker"; Arguments = buildArgs}
                  { Command = "docker"; Arguments = pushArgs} ]
            else
                [ { Command = "docker"; Arguments = buildArgs} ]
        | :? DockerPush as parameters ->
            if parameters.Shared then
                let retagArgs = $"buildx imagetools {parameters.Image}:{parameters.NodeHash} {parameters.Image}:{parameters.Tag}"
                [ { Command = "docker"; Arguments = retagArgs} ]
            else
                let tagArgs = $"tag {parameters.Image}:{parameters.NodeHash} {parameters.Image}:{parameters.Tag}"
                [ { Command = "docker"; Arguments = tagArgs} ]        
        | _ -> ArgumentException($"Unknown action") |> raise
