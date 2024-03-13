namespace Terrabuild.Extensions

open Terrabuild.Extensibility

#nowarn "0077" // op_Explicit

module GradleHelpers =

    [<Literal>]
    let defaultConfiguration = "Debug"


type Gradle() =

    static member __init__ () =
        let projectInfo = { ProjectInfo.Properties = Map.empty
                            ProjectInfo.Ignores = Set []
                            ProjectInfo.Outputs = Set [ "build/classes/" ]
                            ProjectInfo.Dependencies = Set.empty }
        projectInfo


    static member build (configuration: string option) =
        let configuration = configuration |> Option.defaultValue GradleHelpers.defaultConfiguration

        scope Cacheability.Always
        |> andThen "gradlew" $"assemble{configuration}" 

