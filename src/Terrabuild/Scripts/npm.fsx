#if !TERRABUILD_SCRIPT
#r "../bin/Debug/net8.0/Terrabuild.Extensibility.dll"
#endif

open Terrabuild.Extensibility


let private buildCmdLine cmd args =
    { Action.Command = cmd
      Action.Arguments = args
      Action.Cache = Cacheability.Always }


let install () =
    [ buildCmdLine "npm" "ci" ]

let build () =
    [ buildCmdLine "npm" "ci"
      buildCmdLine "npm" "run build" ]

let test () =
    [ buildCmdLine "npm" "ci"
      buildCmdLine "npm" "run test" ]
