namespace Terrabuild.Extensions
open Terrabuild.Extensibility

/// <summary>
/// Provides support for `openapitools`.
/// </summary>
type OpenApi() =

    /// <summary>
    /// Generate api.
    /// </summary>
    static member generate (context: ActionContext) (generator: string) (input: string) (output: string) =
        let args = $"generate -i {input} -g {generator} -o {output}"

        let ops = [
            shellOp "docker-entrypoint.sh" args
        ]
        execRequest Cacheability.Always ops
