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
    static member generate (context: ActionContext) (generator: string) (input: string) (output: string) (properties: Map<string, string>) =
        let props =
            if properties |> Map.isEmpty then ""
            else
                let args = properties |> Seq.map (fun kvp -> $"{kvp.Key}={kvp.Value}") |> String.concat ","
                $"--additional-properties={args}"

        let args = $"generate -i {input} -g {generator} -o {output} {props}"

        let ops = [
            shellOp("docker-entrypoint.sh", args)
        ]
        execRequest(Cacheability.Always, ops, false)
