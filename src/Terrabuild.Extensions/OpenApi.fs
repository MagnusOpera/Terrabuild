namespace Terrabuild.Extensions
open Terrabuild.Extensibility

/// <summary>
/// Provides support for `OpenAPI Generator`.
/// 
/// You must use container `openapitools/openapi-generator-cli` in the extension configuration.
/// </summary>
type OpenApi() =

    /// <summary>
    /// Generate using `openapi-generator-cli`.
    /// </summary>
    static member generate (context: ActionContext) (generator: string) (input: string) (output: string) =
        let args = $"generate -i {input} -g {generator} -o {output}"

        let ops = [
            shellOp("docker-entrypoint.sh", args)
        ]
        execRequest(Cacheability.Always, ops, false)
