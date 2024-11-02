namespace Terrabuild.Extensions

open Terrabuild.Extensibility

/// <summary>
/// Add support for container projects using Docker or Podman/Skopeo.
/// </summary>
type Container() =

    /// <summary>
    /// Build a Dockerfile.
    /// </summary>
    /// <param name="dockerfile" example="&quot;Containerfile&quot;">Use alternative Containerfile. Default is Dockerfile.</param>
    /// <param name="platform" required="false" example="&quot;linux/amd64&quot;">Target platform. Default is host.</param>
    /// <param name="image" required="true" example="&quot;ghcr.io/example/project&quot;">Docker image to build.</param>
    /// <param name="arguments" example="{ configuration: &quot;Release&quot; }">Named arguments to build image (see Dockerfile [ARG](https://docs.docker.com/reference/dockerfile/#arg)).</param> 
    static member build (context: ActionContext) (dockerfile: string option) (platform: string option) (image: string) (arguments: Map<string, string>) (tool: string option) =
        let containerTool =
            match tool with
            | Some "podman" -> "podman"
            | Some "docker" -> "docker"
            | _ -> context.ContainerTool |> Option.defaultValue "docker"

        let dockerfile = dockerfile |> Option.defaultValue "Dockerfile"

        let platform =
            match platform with
            | Some platform -> $" --platform {platform}"
            | _ -> ""

        let args = arguments |> Seq.fold (fun acc kvp -> $"{acc} --build-arg {kvp.Key}=\"{kvp.Value}\"") ""

        let ops = 
            [
                let buildArgs = $"build --file {dockerfile} --tag {image}:{context.Hash}{args}{platform} ."
                if context.CI then
                    shellOp containerTool buildArgs
                    shellOp containerTool $"push {image}:{context.Hash}"
                else
                    shellOp containerTool buildArgs
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
    static member push (context: ActionContext) (image: string) (tag: string) (tool: string option) =
        let containerTool =
            match tool with
            | Some "podman" -> "podman"
            | Some "docker" -> "docker"
            | _ -> context.ContainerTool |> Option.defaultValue "docker"

        let ops =
            match containerTool with
            | "podman" ->
                [
                    if context.CI then
                        shellOp "skopeo" $"copy {image}:{context.Hash} {image}:{tag}"
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