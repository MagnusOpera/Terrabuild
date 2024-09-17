namespace Terrabuild.Extensions
open Terrabuild.Extensibility

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
    static member __defaults__ () =
        let projectInfo = { ProjectInfo.Default
                            with Outputs = Set [ "build/classes/" ] }
        projectInfo

    /// <summary>
    /// Invoke build task `assemble` for `configuration`.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration to invoke `assemble`. Default is `Debug`.</param>
    static member build (context: ActionContext) (configuration: string option) =
        let configuration = configuration |> Option.defaultValue GradleHelpers.defaultConfiguration

        let ops = [ shellOp "gradlew" $"assemble{configuration}" ]

        execRequest Cacheability.Always ops
