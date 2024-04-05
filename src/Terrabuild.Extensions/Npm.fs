namespace Terrabuild.Extensions


open Terrabuild.Extensibility


/// <summary>
/// Provides support for `npm`.
/// </summary>
type Npm() =

    /// <summary>
    /// Provides default values.
    /// </summary>
    /// <param name="ignores" example="[ &quot;node_modules/&quot; ]">Default values.</param>
    /// <param name="outputs" example="[ &quot;dist/&quot; ]">Default values.</param>
    static member __defaults__() =
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
    /// <param name="arguments" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
    static member build (arguments: string option) =
        let args = arguments |> Option.defaultValue ""

        scope Cacheability.Always
        |> andThen "npm" "ci" 
        |> andThen "npm" $"run build -- {args}"

    /// <summary>
    /// Run `test` script.
    /// </summary>
    /// <param name="arguments" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
    static member test (arguments: string option) =
        let args = arguments |> Option.defaultValue ""

        scope Cacheability.Always
        |> andThen "npm" "ci" 
        |> andThen "npm" $"run test -- {args}"
