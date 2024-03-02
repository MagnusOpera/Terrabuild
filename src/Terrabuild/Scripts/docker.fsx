#if !TERRABUILD_SCRIPT
#r "../bin/Debug/net8.0/Terrabuild.Extensibility.dll"
#endif

open Terrabuild.Extensibility


let private buildCmdLine cmd args cache =
    { Action.Command = cmd
      Action.Arguments = args
      Action.Cache = cache }


type Globals = {
    Context: Context
    Dockerfile: string
    Image: string
}


let mutable globals = None



let init (context: Context) (dockerfile: string option) (image: string) =
    let dockerfile = dockerfile |> Option.defaultValue "Dockerfile"
    
    globals <- Some { Context = context
                      Dockerfile = dockerfile
                      Image = image }


let build (arguments: Map<string, string>) =
    let globals = globals.Value
    let args = arguments |> Seq.fold (fun acc kvp -> $"{acc} --build-arg {kvp.Key}=\"{kvp.Value}\"") ""
    let buildArgs = $"build --file {globals.Dockerfile} --tag {globals.Image}:{globals.Context.NodeHash} {args} ."

    if globals.Context.CI then
        let pushArgs = $"push {globals.Image}:{globals.Context.NodeHash}"
        [ buildCmdLine "docker" buildArgs Cacheability.Remote
          buildCmdLine "docker" pushArgs Cacheability.Remote ]
    else
        [ buildCmdLine "docker" buildArgs Cacheability.Local ]
