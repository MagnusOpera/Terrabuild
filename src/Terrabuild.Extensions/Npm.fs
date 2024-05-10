namespace Terrabuild.Extensions


open Terrabuild.Extensibility



module NpmHelpers =
    open System.Text.Json.Serialization
    open System.Text.Json
    open System.Collections.Generic
    open System.IO

    [<CLIMutable>]
    type Package = {
        [<JsonPropertyName("dependencies")>]
        Dependencies: Dictionary<string, string>

        [<JsonPropertyName("devDependencies")>]
        DevDependencies: Dictionary<string, string>
    }

    let findProjectFile (directory: string) =
        let projects =
            System.IO.Directory.EnumerateFiles(directory, "package.json")
        projects |> Seq.exactlyOne

    let findDependencies (projectFile: string) =
        let json = IO.readTextFile projectFile
        let package = JsonSerializer.Deserialize<Package>(json)

        let dependencies = seq {
            if package.Dependencies|> isNull |> not then
                for (KeyValue(_, value)) in package.Dependencies do
                    match value with
                    | String.Regex "^file:(.*)$" [project] -> yield project
                    | _ -> ()

            if package.DevDependencies|> isNull |> not then
                for (KeyValue(_, value)) in package.DevDependencies do
                    match value with
                    | String.Regex "^file:(.*)$" [project] -> yield project
                    | _ -> ()
        }
        Set dependencies

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
