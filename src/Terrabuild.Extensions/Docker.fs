namespace Terrabuild.Extensions

open Terrabuild.Extensibility

/// `terraform` extension provides commands to handle a Terraform project.
type Docker() =

    /// <summary>
    /// Build a Dockerfile.
    /// </summary>
    /// <param name="dockerfile" example="&quot;Dockerfile&quot;">Use alternative Dockerfile. Default is Dockerfile.</param>
    /// <param name="platform" required="false" example="&quot;linux/amd64&quot;">Target platform. Default is host.</param>
    /// <param name="image" required="true" example="&quot;ghcr.io/example/project&quot;">Docker image to build.</param>
    /// <param name="arguments" example="{ configuration: &quot;Release&quot; }">Named arguments to build image (see Dockerfile [ARG](https://docs.docker.com/reference/dockerfile/#arg)).</param> 
    static member build (context: ActionContext) (dockerfile: string option) (platform: string option) (image: string) (arguments: Map<string, string>) =
        let dockerfile = dockerfile |> Option.defaultValue "Dockerfile"
        let nodehash = context.NodeHash

        let platform =
            match platform with
            | Some platform -> $" --platform {platform}"
            | _ -> ""

        let args = arguments |> Seq.fold (fun acc kvp -> $"{acc} --build-arg {kvp.Key}=\"{kvp.Value}\"") ""
        let buildArgs = $"build --file {dockerfile} --tag {image}:{nodehash}{args}{platform} ."

        if context.CI then
            let pushArgs = $"push {image}:{nodehash}"
            scope Cacheability.Remote
            |> andThen "docker" buildArgs
            |> andThen "docker" pushArgs
        else
            scope Cacheability.Local
            |> andThen "docker" buildArgs

    /// <summary>
    /// Push a docker image to registry.
    /// </summary>
    /// <param name="image" required="true" example="&quot;ghcr.io/example/project&quot;">Docker image to build.</param>
    static member push (context: ActionContext) (image: string) =
        let branchOrTag = context.BranchOrTag.Replace("/", "-")
        if context.CI then
            let retagArgs = $"buildx imagetools create -t {image}:{branchOrTag} {image}:{context.NodeHash}"
            scope Cacheability.Remote
            |> andThen "docker" retagArgs
        else
            let tagArgs = $"tag {image}:{context.NodeHash} {image}:{branchOrTag}"
            scope Cacheability.Local
            |> andThen "docker" tagArgs
