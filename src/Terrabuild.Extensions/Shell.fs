namespace Terrabuild.Extensions
open Terrabuild.Extensibility


/// <summary>
/// Provides support for running shell commands.
/// </summary>
type Shell() =

    /// <summary>
    /// Run a shell command using provided arguments.
    /// </summary>
    /// <param name="command">Command to run.</param>
    /// <param name="arguments">Arguments to pass to command.</param>
    static member __dispatch__ (context: ActionContext) (arguments: string option) =
        let arguments = arguments |> Option.defaultValue ""
        scope Cacheability.Always
        |> andThen context.Command arguments 
