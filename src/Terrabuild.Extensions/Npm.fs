namespace Terrabuild.Extensions


open Terrabuild.Extensibility



type Npm() =
    static member install () =
        [ Action.Build "npm" "ci" Cacheability.Always ]

    static member build () =
        [ Action.Build "npm" "ci" Cacheability.Always
          Action.Build "npm" "run build" Cacheability.Always ]

    static member test () =
        [ Action.Build "npm" "ci" Cacheability.Always
          Action.Build "npm" "run test" Cacheability.Always ]
