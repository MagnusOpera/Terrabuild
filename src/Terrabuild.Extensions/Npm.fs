namespace Terrabuild.Extensions


open Terrabuild.Extensibility



type Npm() =

    static member install () =
        scope Cacheability.Always
        |> andThen "npm" "ci"


    static member build () =
        scope Cacheability.Always
        |> andThen "npm" "ci" 
        |> andThen "npm" "run build"


    static member test () =
        scope Cacheability.Always
        |> andThen "npm" "ci" 
        |> andThen "npm" "run test"
