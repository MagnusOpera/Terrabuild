namespace Terrabuild.Extensions
open Terrabuild.Extensibility


type Shell() =

    static member echo (message: string) =
        [ Action.Build "echo" message Cacheability.Always ]

    static member exec (command:string) (arguments: string) =
        [ Action.Build command arguments Cacheability.Always ]
