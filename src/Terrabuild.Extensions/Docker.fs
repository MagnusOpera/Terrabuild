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

        let platform =
            match platform with
            | Some platform -> $" --platform {platform}"
            | _ -> ""

        let args = arguments |> Seq.fold (fun acc kvp -> $"{acc} --build-arg {kvp.Key}=\"{kvp.Value}\"") ""

        let ops = 
            context.Projects 
            |> Map.map (fun nodehash _ -> [
                let buildArgs = $"build --file {dockerfile} --tag {image}:{nodehash}{args}{platform} ."
                if context.CI then
                    shellOp "docker" buildArgs
                    shellOp "docker" $"push {image}:{nodehash}"
                else
                    shellOp "docker" buildArgs])
            |> Each

        let cacheability =
            if context.CI then Cacheability.Remote
            else Cacheability.Local

        execRequest cacheability [] ops


    /// <summary>
    /// Push a docker image to registry.
    /// </summary>
    /// <param name="image" required="true" example="&quot;ghcr.io/example/project&quot;">Docker image to build.</param>
    /// <param name="tag" required="false" example="&quot;1.2.3-stable&quot;">Apply tag on image (use branch or tag otherwise).</param>
    static member push (context: ActionContext) (image: string) (tag: string option)=
        let imageTag =
            match tag with
            | Some tag -> tag
            | _ -> context.BranchOrTag.Replace("/", "-")

        let ops =
            context.Projects
            |> Map.map (fun nodehash _ -> [
                if context.CI then
                    shellOp "docker" $"buildx imagetools create -t {image}:{imageTag} {image}:{nodehash}"
                else
                    shellOp "docker" $"tag {image}:{nodehash} {image}:{imageTag}"])
            |> Each

        let cacheability =
            if context.CI then Cacheability.Remote
            else Cacheability.Local

        execRequest cacheability [] ops
