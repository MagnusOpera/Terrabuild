namespace Terrabuild.Extensions


open Terrabuild.Extensibility



type Npm() =

    static member Install () =
        [ Action.Build "npm" "ci" Cacheability.Always ]


    static member Build () =
        [ Action.Build "npm" "ci" Cacheability.Always
          Action.Build "npm" "run build" Cacheability.Always ]


    static member Test () =
        [ Action.Build "npm" "ci" Cacheability.Always
          Action.Build "npm" "run test" Cacheability.Always ]
