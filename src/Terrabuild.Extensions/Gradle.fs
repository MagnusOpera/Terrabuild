namespace Terrabuild.Extensions

open Terrabuild.Extensibility

#nowarn "0077" // op_Explicit

module GradleHelpers =

    [<Literal>]
    let defaultConfiguration = "Debug"


/// <summary>
/// Add support for Gradle build.
/// </summary>
type Gradle() =

    /// <summary>
    /// Provides default values for project.
    /// </summary>
    /// <param name="outputs" example="[ &quot;build/classes/&quot; ]">Default values.</param>
    static member __init__ () =
        let projectInfo = { ProjectInfo.Default with Outputs = Set [ "build/classes/" ] }
        projectInfo

    /// <summary>
    /// Invoke build task `assemble` for `configuration`.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration to invoke `assemble`. Default is `Debug`.</param>
    static member build (configuration: string option) =
        let configuration = configuration |> Option.defaultValue GradleHelpers.defaultConfiguration

        scope Cacheability.Always
        |> andThen "gradlew" $"assemble{configuration}" 
