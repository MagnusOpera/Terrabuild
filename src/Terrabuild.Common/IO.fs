module IO
open System.IO
open Microsoft.Extensions.FileSystemGlobbing
open Collections
open System

let chmod permissions (path: string) =
    File.SetUnixFileMode(path, permissions)

let createDirectory (path: string) =
    Directory.CreateDirectory(path) |> ignore

let readTextFile filename =
    filename |> File.ReadAllText

let writeTextFile filename (content: string) =
    File.WriteAllText(filename, content)

let writeLines filename (lines: string seq) =
    File.WriteAllLines(filename, lines)

let appendLinesFile filename content =
    File.AppendAllLines(filename, content)

let getTempFilename () =
    Path.GetTempFileName()

let getFilename (path: string) =
    Path.GetFileName(path)

let exists path =
    Path.Exists(path)

let moveFile source destination =
    File.Move(source, destination, true)

let deleteAny entry =
    match entry with
    | FS.File file -> File.Delete(file)
    | FS.Directory directory -> Directory.Delete(directory, true)
    | _ -> ()

let size file =
    FileInfo(file).Length |> int

let enumerateDirs rootDir =
    Directory.EnumerateDirectories(rootDir)

let enumerateFiles rootdir =
    Directory.EnumerateFiles(rootdir, "*", SearchOption.AllDirectories)
    |> List.ofSeq

let enumerateFilesBut (matches: string seq) (ignores: string set) rootdir =
    let matcher = Matcher()
    matcher.AddIncludePatterns(matches)
    matcher.AddExcludePatterns(ignores)

    let result =
        matcher.GetResultsInFullPath(rootdir)
        |> List.ofSeq
    result

let copyFiles (targetDir: string) (baseDir: string) (entries: string list) =
    for entry in entries do
        let relative = FS.relativePath baseDir entry
        let target = FS.combinePath targetDir relative
        let targetDir = FS.parentDirectory target
        Directory.CreateDirectory targetDir |> ignore
        File.Copy(entry, target, true)
    if entries |> List.isEmpty then None
    else Some targetDir



type Snapshot = {
    TimestampedFiles: Map<string, DateTime>
}
with
    static member Empty = { TimestampedFiles = Map.empty }

    static member (-) (afterOutputs: Snapshot, beforeOutputs: Snapshot) =
        let newOutputs =
            afterOutputs.TimestampedFiles
            |> Seq.choose (fun afterOutput ->
                match beforeOutputs.TimestampedFiles |> Map.tryFind afterOutput.Key with
                | Some prevWriteDate when afterOutput.Value = prevWriteDate -> None
                | _ -> Some afterOutput.Key)
            |> List.ofSeq
        newOutputs

let createSnapshot outputs projectDirectory =
    let enumerateFilesMatch (matches: string seq) rootdir =
        let matcher = Matcher()
        matcher.AddIncludePatterns(matches)

        let result =
            matcher.GetResultsInFullPath(rootdir)
            |> List.ofSeq
        result

    let files =
        enumerateFilesMatch outputs projectDirectory
        |> Seq.map (fun output -> output, System.IO.File.GetLastWriteTimeUtc output)
        |> Map
    { TimestampedFiles = files }
