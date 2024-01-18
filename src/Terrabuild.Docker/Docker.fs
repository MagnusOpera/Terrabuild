namespace Extensions
open System
open Extensions
open System.ComponentModel.Composition

type DockerBuild = {
    NodeHash: string
    Image: string
    Arguments: Map<string, string>
}

type DockerPush = {
    NodeHash: string
    Image: string
}

type Docker(context: Context) =
    let dockerfile =
        match context.With with
        | Some dockerfile -> dockerfile
        | _ -> "Dockerfile"

    let buildCmdLine cmd args cache =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = cache }

    interface IExtension with
        member _.Container = None // Some "docker:24.0-dind"

        member _.Dependencies = []

        member _.Outputs = []

        member _.Ignores = []

        member _.GetStepParameters action =
            match action with
            | "build" -> Some typeof<DockerBuild>
            | "push" -> Some typeof<DockerPush>
            | _ -> ArgumentException($"Unknown action {action}") |> raise

        member _.BuildStepCommands (action, parameters) =
            match parameters, action with
            | :? DockerBuild as parameters, _ ->
                let args = parameters.Arguments |> Seq.fold (fun acc kvp -> $"{acc} --build-arg {kvp.Key}=\"{kvp.Value}\"") ""
                let buildArgs = $"build --file {dockerfile} --tag {parameters.Image}:{parameters.NodeHash} {args} ."

                if context.CI then
                    let pushArgs = $"push {parameters.Image}:{parameters.NodeHash}"
                    [ buildCmdLine "docker" buildArgs Cacheability.Remote
                      buildCmdLine "docker" pushArgs Cacheability.Remote ]
                else
                    [ buildCmdLine "docker" buildArgs Cacheability.Local ]
            | :? DockerPush as parameters, _ ->
                if context.CI then
                    let retagArgs = $"buildx imagetools create -t {parameters.Image}:$(terrabuild_branch_or_tag) {parameters.Image}:{parameters.NodeHash}"
                    [ buildCmdLine "docker" retagArgs Cacheability.Remote ]
                else
                    let tagArgs = $"tag {parameters.Image}:{parameters.NodeHash} {parameters.Image}:$(terrabuild_branch_or_tag)"
                    [ buildCmdLine "docker" tagArgs Cacheability.Local ]
            | _ -> ArgumentException($"Unknown action") |> raise


[<Export("docker", typeof<IExtensionFactory>)>]
type DockerFactory() =
    interface IExtensionFactory with
        member _.Create ctx =
            Docker(ctx)
