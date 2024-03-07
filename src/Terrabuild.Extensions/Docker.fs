namespace Terrabuild.Extensions

open Terrabuild.Extensibility


type Docker() =

    static member Build (context: ActionContext) (dockerfile: string option) (image: string) (arguments: Map<string, string>) =
        let dockerfile = dockerfile |> Option.defaultValue "Dockerfile"
        let nodehash = context.NodeHash

        let args = arguments |> Seq.fold (fun acc kvp -> $"{acc} --build-arg {kvp.Key}=\"{kvp.Value}\"") ""
        let buildArgs = $"build --file {dockerfile} --tag {image}:{nodehash} {args} ."

        if context.CI then
            let pushArgs = $"push {image}:{nodehash}"
            scope Cacheability.Remote
            |> andThen "docker" buildArgs
            |> andThen "docker" pushArgs
        else
            scope Cacheability.Local
            |> andThen "docker" buildArgs


    static member Push (context: ActionContext) (image: string) =
        if context.CI then
            let retagArgs = $"buildx imagetools create -t {image}:{context.BranchOrTag} {image}:{context.NodeHash}"
            scope Cacheability.Remote
            |> andThen "docker" retagArgs
        else
            let tagArgs = $"tag {image}:{context.NodeHash} {image}:{context.BranchOrTag}"
            scope Cacheability.Local
            |> andThen "docker" tagArgs

