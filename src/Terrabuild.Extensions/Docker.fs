namespace Terrabuild.Extensions

open Terrabuild.Extensibility

/// <summary>
/// Add support for Docker projects.
/// </summary>
type Docker() =

    /// <summary>
    /// Build a Dockerfile.
    /// </summary>
    /// <param name="dockerfile" example="&quot;Dockerfile&quot;">Use alternative Dockerfile. Default is Dockerfile.</param>
    /// <param name="platforms" required="false" example="[ &quot;linux/amd64&quot; ]">List of platform to build. Default is host.</param>
    /// <param name="image" required="true" example="&quot;ghcr.io/example/project&quot;">Docker image to build.</param>
    /// <param name="arguments" example="{ configuration: &quot;Release&quot; }">Named arguments to build image (see Dockerfile [ARG](https://docs.docker.com/reference/dockerfile/#arg)).</param> 
    static member build (context: ActionContext) (dockerfile: string option) (platforms: string list option) (image: string) (arguments: Map<string, string>) =
        let dockerfile = dockerfile |> Option.defaultValue "Dockerfile"

        let platformArgs =
            match platforms with
            | None -> ""
            | Some platforms ->
                platforms
                |> Seq.fold (fun acc platform -> $"{acc} --platform {platform}") ""

        let args = arguments |> Seq.fold (fun acc kvp -> $"{acc} --build-arg {kvp.Key}=\"{kvp.Value}\"") ""

        let ops = 
            [
                let pushArgs = if context.CI then " --push" else ""
                let buildArgs = $"buildx build --file {dockerfile} --tag {image}:{context.Hash}{args}{platformArgs}{pushArgs} ."
                shellOp "docker" buildArgs
            ]

        let cacheability =
            if context.CI then Cacheability.Remote
            else Cacheability.Local

        execRequest cacheability ops


    /// <summary>
    /// Push target container image to registry.
    /// </summary>
    /// <param name="image" required="true" example="&quot;ghcr.io/example/project&quot;">Docker image to build.</param>
    /// <param name="tag" required="true" example="&quot;1.2.3-stable&quot;">Apply tag on image (use branch or tag otherwise).</param>
    static member push (context: ActionContext) (image: string) (tag: string) =
        let ops =
            [
                if context.CI then
                    shellOp "docker" $"buildx imagetools create -t {image}:{tag} {image}:{context.Hash}"
                else
                    shellOp "docker" $"tag {image}:{context.Hash} {image}:{tag}"
            ]

        let cacheability =
            if context.CI then Cacheability.Remote
            else Cacheability.Local

        execRequest cacheability ops
