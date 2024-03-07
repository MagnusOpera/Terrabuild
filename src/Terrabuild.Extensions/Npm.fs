namespace Terrabuild.Extensions


open Terrabuild.Extensibility



type Npm() =

    static member Install () =
        scope Cacheability.Always
        |> andThen "npm" "ci"


    static member Build () =
        scope Cacheability.Always
        |> andThen "npm" "ci" 
        |> andThen "npm" "run build"


    static member Test () =
        scope Cacheability.Always
        |> andThen "npm" "ci" 
        |> andThen "npm" "run test"
