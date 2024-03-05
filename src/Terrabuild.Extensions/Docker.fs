namespace Terrabuild.Extensions

open Terrabuild.Extensibility


type Docker() =

    static member build (context: ActionContext) (dockerfile: string option) (image: string) (arguments: Map<string, string>) =
        let dockerfile = dockerfile |> Option.defaultValue "Dockerfile"
        let nodehash = context.NodeHash

        let args = arguments |> Seq.fold (fun acc kvp -> $"{acc} --build-arg {kvp.Key}=\"{kvp.Value}\"") ""
        let buildArgs = $"build --file {dockerfile} --tag {image}:{nodehash} {args} ."

        if context.CI then
            let pushArgs = $"push {image}:{nodehash}"
            [ Action.Build "docker" buildArgs Cacheability.Remote
              Action.Build "docker" pushArgs Cacheability.Remote ]
        else
            [ Action.Build "docker" buildArgs Cacheability.Local ]

    static member push (context: ActionContext) (image: string) =
        if context.CI then
            let retagArgs = $"buildx imagetools create -t {image}:{context.BranchOrTag} {image}:{context.NodeHash}"
            [ Action.Build "docker" retagArgs Cacheability.Remote ]
        else
            let tagArgs = $"tag {image}:{context.NodeHash} {image}:{context.BranchOrTag}"
            [ Action.Build "docker" tagArgs Cacheability.Local ]
