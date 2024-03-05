module Script

#if !TERRABUILD_SCRIPT
#r "../bin/Debug/net8.0/Terrabuild.Extensibility.dll"
#endif

open Terrabuild.Extensibility


let private buildCmdLine cmd args =
    { Action.Command = cmd
      Action.Arguments = args
      Action.Cache = Cacheability.Always }


let __dispatch__ (context: ActionContext) (variables: Map<string, string>) =
    let args = variables |> Seq.fold (fun acc kvp -> $"{acc} {kvp.Key}=\"{kvp.Value}\"") $"{context.Command}"
    [ buildCmdLine "make" args ]
