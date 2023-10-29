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

type Docker(context) =
    inherit Extension(context)

    let dockerfile =
        match context.With with
        | Some dockerfile -> dockerfile
        | _ -> "Dockerfile"

    let buildCmdLine cmd args cache =
        { Extensions.CommandLine.Command = cmd
          Extensions.CommandLine.Arguments = args
          Extensions.CommandLine.Cache = cache }

    override _.Container = None

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

            if context.CI then
                let pushArgs = $"push {parameters.Image}:{parameters.NodeHash}"
                [ buildCmdLine "docker" buildArgs Cacheability.Remote
                  buildCmdLine "docker" pushArgs Cacheability.Remote ]
            else
                [ buildCmdLine "docker" buildArgs Cacheability.Local ]
        | :? DockerPush as parameters ->
            if context.CI then
                let retagArgs = $"buildx imagetools create -t {parameters.Image}:$(terrabuild_branch_or_tag) {parameters.Image}:{parameters.NodeHash}"
                [ buildCmdLine "docker" retagArgs Cacheability.Remote ]
            else
                let tagArgs = $"tag {parameters.Image}:{parameters.NodeHash} {parameters.Image}:$(terrabuild_branch_or_tag)"
                [ buildCmdLine "docker" tagArgs Cacheability.Local ]        
        | _ -> ArgumentException($"Unknown action") |> raise
