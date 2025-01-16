namespace Terrabuild.Extensions
open Terrabuild.Extensibility

/// <summary>
/// `null` extension is for testing purpose. It supports fake `init` and fake `dispatch`.
/// </summary>
type Null() =

    /// <summary>
    /// Fake init.
    /// </summary>
    static member __defaults__ (context: ExtensionContext) =
        ProjectInfo.Default

    /// <summary>
    /// Fake dispatch.
    /// </summary>
    static member __dispatch__ (context: ActionContext) =
        ()

    /// <summary>
    /// Fake action.
    /// </summary>
    static member fake (context: ActionContext) =
        ()
