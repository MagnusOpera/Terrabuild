module Helpers.IO
open System.IO

let combine parent child =
    Path.Combine(parent, child)

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
