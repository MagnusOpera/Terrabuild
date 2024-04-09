module FS
open System.IO


let fullPath path =
    Path.GetFullPath(path)

let combinePath parent child =
    Path.Combine(parent, child)

let relativePath fromDir toDir =
    Path.GetRelativePath(fromDir, toDir)

let parentDirectory (path: string) =
    Path.GetDirectoryName(path)

let (|File|Directory|None|) entry =
    if File.Exists(entry) then File entry
    elif Directory.Exists(entry) then Directory entry
    else None entry

let workspaceRelative (workspaceDir: string) (currentDir: string) (relativeOrAbsolute: string) =
    match relativeOrAbsolute with
    | String.Regex "^/(.*)$" [ absolute ] -> absolute
    | relative -> combinePath currentDir relative |> relativePath workspaceDir
