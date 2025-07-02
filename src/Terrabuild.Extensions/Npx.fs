namespace Terrabuild.Extensions
open Terrabuild.Extensibility


/// <summary>
/// Provides support for running npx commands.
/// </summary>
type Npx() =

    /// <summary>
    /// Run an npx command.
    /// </summary>
    /// <param name="arguments" example="&quot;hello-world-npm&quot;">Arguments to pass to npx.</param> 
    static member __dispatch__ (context: ActionContext) (arguments: string option) =
        let cmd = context.Command
        let arguments = arguments |> Option.defaultValue ""

        let ops = [
            shellOp("npm", $"--yes {cmd} {arguments}")
        ]
        execRequest(Cacheability.Always, ops, false)
