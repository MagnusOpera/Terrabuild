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
    static member __defaults__(context: ExtensionContext) =
        let projectFile = NpmHelpers.findProjectFile context.Directory
        let dependencies = projectFile |> NpmHelpers.findDependencies 
        let projectInfo = 
            { ProjectInfo.Default
              with Ignores = Set [ "node_modules/" ]
                   Outputs = Set [ "dist/" ]
                   Dependencies = dependencies }
        projectInfo


    /// <summary>
    /// Install packages using lock file.
    /// </summary>
    static member install (context: ActionContext) =
        let ops = [ shellOp "npm" "ci" ]
        execRequest Cacheability.Always ops


    /// <summary>
    /// Run `build` script.
    /// </summary>
    /// <param name="arguments" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
    static member build (context: ActionContext) (arguments: string option) =
        let args = arguments |> Option.defaultValue ""

        let ops = [
            shellOp "npm" "ci"
            shellOp "npm" $"run build -- {args}"   
        ]
        execRequest Cacheability.Always ops


    /// <summary>
    /// Run `test` script.
    /// </summary>
    /// <param name="arguments" example="&quot;--port=1337&quot;">Arguments to pass to target.</param> 
    static member test (context: ActionContext) (arguments: string option) =
        let args = arguments |> Option.defaultValue ""

        let ops = [
            shellOp "npm" "ci"
            shellOp "npm" $"run test -- {args}"   
        ]
        execRequest Cacheability.Always ops

    /// <summary>
    /// Run `run` script.
    /// </summary>
    /// <param name="arguments" example="&quot;build-prod&quot;">Arguments to pass to target.</param> 
    static member run (context: ActionContext) (command: string) (arguments: string option) =
        let args = arguments |> Option.defaultValue ""

        let ops = [
            shellOp "npm" $"run {command} -- {args}"
        ]
        execRequest Cacheability.Always ops
