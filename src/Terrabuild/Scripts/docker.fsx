#if !TERRABUILD_SCRIPT
#r "../bin/Debug/net8.0/Terrabuild.Extensibility.dll"
#endif

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
