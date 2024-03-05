module Docker
type Dummy = interface end

open Terrabuild.Extensibility


let private buildCmdLine cmd args cache =
    { Action.Command = cmd
      Action.Arguments = args
      Action.Cache = cache }


let build (context: ActionContext) (dockerfile: string option) (image: string) (arguments: Map<string, string>) =
    let dockerfile = dockerfile |> Option.defaultValue "Dockerfile"
    let nodehash = context.NodeHash

    let args = arguments |> Seq.fold (fun acc kvp -> $"{acc} --build-arg {kvp.Key}=\"{kvp.Value}\"") ""
    let buildArgs = $"build --file {dockerfile} --tag {image}:{nodehash} {args} ."

    if context.CI then
        let pushArgs = $"push {image}:{nodehash}"
        [ buildCmdLine "docker" buildArgs Cacheability.Remote
          buildCmdLine "docker" pushArgs Cacheability.Remote ]
    else
        [ buildCmdLine "docker" buildArgs Cacheability.Local ]

let push (context: ActionContext) (image: string) =
    if context.CI then
        let retagArgs = $"buildx imagetools create -t {image}:{context.BranchOrTag} {image}:{context.NodeHash}"
        [ buildCmdLine "docker" retagArgs Cacheability.Remote ]
    else
        let tagArgs = $"tag {image}:{context.NodeHash} {image}:{context.BranchOrTag}"
        [ buildCmdLine "docker" tagArgs Cacheability.Local ]
