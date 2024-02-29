module Extensions.System
open Terrabuild.Extensibility


let private buildCmdLine cmd args =
    { Action.Command = cmd
      Action.Arguments = args
      Action.Cache = Cacheability.Always }


let exec (command: string) (arguments: string option) =
    let args = arguments |> Option.defaultValue ""
    [ buildCmdLine command args ]

let echo (message: string) =
    [ buildCmdLine "echo" message ]
