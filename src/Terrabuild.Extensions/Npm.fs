namespace Terrabuild.Extensions


open Terrabuild.Extensibility


/// <summary>
/// Provides support for `npm`.
/// </summary>
type Npm() =

    /// <summary>
    /// Provides default values.
    /// </summary>
    /// <param name="ignores">Ignores `node_modules/`</param>
    /// <param name="outputs">Includes `dist/`</param>
    static member __init__() =
        let projectInfo = 
            { ProjectInfo.Default
              with Ignores = Set [ "node_modules/" ]
                   Outputs = Set [ "dist/" ] }
        projectInfo

    /// <summary>
    /// Install packages using lock file.
    /// </summary>
    static member install () =
        scope Cacheability.Always
        |> andThen "npm" "ci"

    /// <summary>
    /// Run `build` script.
    /// </summary>
    static member build () =
        scope Cacheability.Always
        |> andThen "npm" "ci" 
        |> andThen "npm" "run build"

    /// <summary>
    /// Run `test` script.
    /// </summary>
    static member test () =
        scope Cacheability.Always
        |> andThen "npm" "ci" 
        |> andThen "npm" "run test"
