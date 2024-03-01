#r "Terrabuild.Extensibility.dll"
open Terrabuild.Extensibility


let buildCmdLine cmd args =
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
