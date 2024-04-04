module IO
open System.IO
open Microsoft.Extensions.FileSystemGlobbing
open Collections

let fullPath path =
    Path.GetFullPath(path)

let combinePath parent child =
    Path.Combine(parent, child)

let relativePath fromDir toDir =
    Path.GetRelativePath(fromDir, toDir)

let parentDirectory (path: string) =
    Path.GetDirectoryName(path)

let createDirectory (path: string) =
    Directory.CreateDirectory(path) |> ignore

let readTextFile filename =
    filename |> File.ReadAllText

let writeTextFile filename content =
    File.WriteAllText(filename, content)

let getTempFilename () =
    Path.GetTempFileName()

let getFilename (p: string) =
    Path.GetFileName(p)

let moveFile source destination =
    File.Move(source, destination, true)

let (|File|Directory|None|) entry =
    if File.Exists(entry) then File entry
    elif Directory.Exists(entry) then Directory entry
    else None

let deleteAny entry =
    match entry with
    | File file -> File.Delete(file)
    | Directory directory -> Directory.Delete(directory, true)
    | _ -> ()

let enumerateDirs rootDir =
    Directory.EnumerateDirectories(rootDir)

let enumerateFiles rootdir =
    Directory.EnumerateFiles(rootdir, "*", SearchOption.AllDirectories)
    |> List.ofSeq

let enumerateMatchingFiles pattern rootdir =
    Directory.EnumerateFiles(rootdir, pattern, SearchOption.AllDirectories)
    |> List.ofSeq

let enumerateFilesBut (ignores: string set) rootdir =
    let matcher = Matcher()
    matcher.AddInclude("**/*").AddExcludePatterns(ignores)

    let result =
        matcher.GetResultsInFullPath(rootdir)
        |> List.ofSeq
    result

let enumerateFilesMatch (matches: string set) rootdir =
    let matcher = Matcher()
    matcher.AddIncludePatterns(matches)

    let result =
        matcher.GetResultsInFullPath(rootdir)
        |> List.ofSeq
    result


let copyFiles (targetDir: string) (baseDir: string) (entries: string list) =
    for entry in entries do
        let relative = relativePath baseDir entry
        let target = combinePath targetDir relative
        let targetDir = parentDirectory target
        Directory.CreateDirectory targetDir |> ignore
        File.Copy(entry, target, true)
    if entries |> List.isEmpty then None
    else Some targetDir
