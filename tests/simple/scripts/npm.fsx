#if !TERRABUILD_SCRIPT
#r "../../../src/Terrabuild/bin/Debug/net8.0/Terrabuild.Extensibility.dll"
#endif

open Terrabuild.Extensibility


let __defaults__() =
    let projectInfo = 
        { ProjectInfo.Default
            with Ignores = Set [ "node_modules/" ]
                 Outputs = Set [ "dist/" ] }
    projectInfo

let install () =
    scope Cacheability.Always
    |> andThen "npm" "ci"

let build (arguments: string option) =
    let args = arguments |> Option.defaultValue ""

    scope Cacheability.Always
    |> andThen "npm" "ci" 
    |> andThen "npm" $"run build -- {args}"

let test (arguments: string option) =
    let args = arguments |> Option.defaultValue ""

    scope Cacheability.Always
    |> andThen "npm" "ci" 
    |> andThen "npm" $"run test -- {args}"
