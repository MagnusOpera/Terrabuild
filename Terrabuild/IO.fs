module IO
open System.IO

let combine parent child =
    Path.Combine(parent, child)

let relativePath fromDir toDir =
    Path.GetRelativePath(fromDir, toDir)

let parentDirectory (path: string) =
    Path.GetDirectoryName(path)

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

let enumerateFilesBut ignore dir =
    let rec enumerateFilesBut dir =
        seq {
            if ignore |> Set.contains dir |> not then
                let files = Directory.EnumerateFiles(dir)
                for file in files do
                    if ignore |> Set.contains file |> not then
                        yield file

                let dirs = Directory.EnumerateDirectories(dir)
                for dir in dirs do
                    yield! enumerateFilesBut dir                
        }

    let res = enumerateFilesBut dir |> List.ofSeq
    res

