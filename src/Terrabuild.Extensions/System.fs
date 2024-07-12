namespace Terrabuild.Extensions

/// <summary>
/// Provides support for running system commands.
/// </summary>
type System() =

    /// <summary>
    /// Write `body` string to text `file`.
    /// </summary>
    /// <param name="file" example="dist/version.txt">Example.</param>
    /// <param name="body" example="&quot;Hello Terrabuild&quot;">Body of text file.</param>
    static member write (file: string) (body: string) =
        System.IO.File.WriteAllText(file, body)
