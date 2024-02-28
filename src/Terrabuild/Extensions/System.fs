module Extensions.System
open Terrabuild.Extensibility


let private buildCmdLine cmd args =
    { Step.Command = cmd
      Step.Arguments = args
      Step.Cache = Cacheability.Always }


let exec (command: string) (arguments: string option) =
    let args = arguments |> Option.defaultValue ""
    [ buildCmdLine command args ]

let echo (message: string) =
    [ buildCmdLine "echo" message ]
