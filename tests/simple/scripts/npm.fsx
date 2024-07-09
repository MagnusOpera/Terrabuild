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
    let ops = All [ shellOp "npm" "ci" ]
    execRequest Cacheability.Always [] ops

let build (arguments: string option) =
    let args = arguments |> Option.defaultValue ""

    let ops = All [
        shellOp "npm" "ci"
        shellOp "npm" $"run build -- {args}"
    ]
    execRequest Cacheability.Always [] ops

let test (arguments: string option) =
    let args = arguments |> Option.defaultValue ""

    let ops = All [
        shellOp "npm" "ci"
        shellOp "npm" $"run test -- {args}"
    ]
    execRequest Cacheability.Always [] ops
