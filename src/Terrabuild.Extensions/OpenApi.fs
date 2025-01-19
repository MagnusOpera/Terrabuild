namespace Terrabuild.Extensions
open Terrabuild.Extensibility

/// <summary>
/// Provides support for `OpenAPI Generator`.
/// </summary>
type OpenApi() =

    /// <summary>
    /// Provides default values for project.
    /// </summary>
    /// <param name="outputs" example="[ &quot;src&quot; ]">Default values.</param>
    static member __defaults__ () =
        let projectInfo = { ProjectInfo.Default
                            with Container = Some "openapitools/openapi-generator-cli" }
        projectInfo


    /// <summary>
    /// Generate using `openapi-generator-cli`.
    /// </summary>
    static member generate (context: ActionContext) (generator: string) (input: string) (output: string) =
        let args = $"generate -i {input} -g {generator} -o {output}"

        let ops = [
            shellOp "docker-entrypoint.sh" args
        ]
        execRequest Cacheability.Always ops
