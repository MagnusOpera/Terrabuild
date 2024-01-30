namespace Terrabuild.Docker.Build
open Terrabuild.Extensibility
open Helpers

type Arguments = {
    NodeHash: string
    Image: string
    Arguments: Map<string, string>
}

type Command(context: Context) =
    let dockerfile =
        match context.With with
        | Some dockerfile -> dockerfile
        | _ -> "Dockerfile"

    interface ICommandBuilder<Arguments> with
        member _.GetSteps parameters =
            let args = parameters.Arguments |> Seq.fold (fun acc kvp -> $"{acc} --build-arg {kvp.Key}=\"{kvp.Value}\"") ""
            let buildArgs = $"build --file {dockerfile} --tag {parameters.Image}:{parameters.NodeHash} {args} ."

            if context.CI then
                let pushArgs = $"push {parameters.Image}:{parameters.NodeHash}"
                [ buildCmdLine "docker" buildArgs Cacheability.Remote
                  buildCmdLine "docker" pushArgs Cacheability.Remote ]
            else
                [ buildCmdLine "docker" buildArgs Cacheability.Local ]
