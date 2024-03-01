#r "Terrabuild.Extensibility.dll"
open Terrabuild.Extensibility


let buildCmdLine cmd args =
    { Action.Command = cmd
      Action.Arguments = args
      Action.Cache = Cacheability.Always }

let build (nodeHash: string) (image: string) (arguments: Map<string, string>) =
    let args = arguments |> Seq.fold (fun acc kvp -> $"{acc} --build-arg {kvp.Key}=\"{kvp.Value}\"") ""
    let buildArgs = $"build --file {dockerfile} --tag {parameters.Image}:{parameters.NodeHash} {args} ."

    if context.CI then
        let pushArgs = $"push {parameters.Image}:{parameters.NodeHash}"
        [ buildCmdLine "docker" buildArgs Cacheability.Remote
            buildCmdLine "docker" pushArgs Cacheability.Remote ]
    else
        [ buildCmdLine "docker" buildArgs Cacheability.Local ]
