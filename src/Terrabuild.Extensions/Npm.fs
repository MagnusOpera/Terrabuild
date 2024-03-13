namespace Terrabuild.Extensions


open Terrabuild.Extensibility



type Npm() =

    static member __init__() =
        let projectInfo = { ProjectInfo.Properties = Map.empty
                            ProjectInfo.Ignores = Set [ "node_modules/" ]
                            ProjectInfo.Outputs = Set [ "dist/" ]
                            ProjectInfo.Dependencies = Set.empty }
        projectInfo


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
