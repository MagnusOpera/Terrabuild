module Shell
type Dummy = interface end


open Terrabuild.Extensibility


let buildCmdLine cmd args =
    { Action.Command = cmd
      Action.Arguments = args
      Action.Cache = Cacheability.Always }

let echo (message: string) =
    [ buildCmdLine "echo" message ]

let exec (command:string) (arguments: string) =
    [ buildCmdLine command arguments ]
