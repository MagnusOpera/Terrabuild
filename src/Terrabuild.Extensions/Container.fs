namespace Terrabuild.Extensions

open Terrabuild.Extensibility

/// <summary>
/// Add support for container projects using Docker (default) or Podman/Skopeo.
/// </summary>
type Container() =

    /// <summary>
    /// Build a Dockerfile.
    /// </summary>
    /// <param name="dockerfile" example="&quot;Containerfile&quot;">Use alternative Containerfile. Default is Dockerfile.</param>
    /// <param name="platforms" required="false" example="[ &quot;linux/amd64&quot; ]">List of platform to build. Default is host.</param>
    /// <param name="image" required="true" example="&quot;ghcr.io/example/project&quot;">Docker image to build.</param>
    /// <param name="arguments" example="{ configuration: &quot;Release&quot; }">Named arguments to build image (see Dockerfile [ARG](https://docs.docker.com/reference/dockerfile/#arg)).</param> 
    /// <param name="tool" required="false" example="&quot;podman&quot;">Build tool to use: docker (default) or podman</param>
    static member build (context: ActionContext) (dockerfile: string option) (platforms: string list option) (image: string) (arguments: Map<string, string>) (tool: string option) =
        let dockerfile = dockerfile |> Option.defaultValue "Dockerfile"

        let platformArgs =
            match platforms with
            | None -> ""
            | Some platforms ->
                platforms
                |> Seq.fold (fun acc platform -> $"{acc} --platform {platform}") ""

        let args = arguments |> Seq.fold (fun acc kvp -> $"{acc} --build-arg {kvp.Key}=\"{kvp.Value}\"") ""

        let ops = [
                match tool with
                | Some "podman" ->
                    let buildArgs = $"build --file {dockerfile} --tag {image}:{context.Hash}{args}{platformArgs} ."
                    if context.CI then
                        shellOp "podman" buildArgs
                        shellOp "podman" $"push {image}:{context.Hash}"
                    else
                        shellOp "podman" buildArgs
                | _ ->
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
    /// <param name="image" required="true" example="&quot;ghcr.io/example/project&quot;">Container image to build.</param>
    /// <param name="tag" required="true" example="&quot;1.2.3-stable&quot;">Apply tag on image (use branch or tag otherwise).</param>
    /// <param name="tool" required="false" example="&quot;podman&quot;">Build tool to use: docker (default) or podman</param>
    static member push (context: ActionContext) (image: string) (tag: string) (tool: string option) =
        let ops =
            match tool with
            | Some "podman" ->
                [
                    if context.CI then
                        shellOp "skopeo" $"copy --multi-arch all {image}:{context.Hash} {image}:{tag}"
                    else
                        shellOp "podman" $"tag {image}:{context.Hash} {image}:{tag}"
                ]
            | _ ->
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
