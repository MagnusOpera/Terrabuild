#if !TERRABUILD_SCRIPT
#r "../../Terrabuild/bin/Debug/net8.0/Terrabuild.Extensibility.dll"
#endif

open Terrabuild.Extensibility
open System.IO
open System.Text.RegularExpressions

let private (|Regex|_|) pattern input =
    let m = Regex.Match(input, pattern)
    if m.Success then
        List.tail [ for g in m.Groups -> g.Value ] |> Some
    else
        None

let private findProject = function
    | Regex "^Project\(.*\) = \".*\", \"(.*)\", .*$" [projectFile] ->
        Some projectFile
    | _ ->
        None

let __init__ (context: InitContext) =
    let dependencies =
        Directory.EnumerateFiles(context.Directory, "*.sln") |> Seq.head
        |> File.ReadLines
        |> Seq.choose findProject

    { ProjectInfo.Default
      with Ignores = Set.empty
           Outputs = Set.empty
           Dependencies = Set dependencies }
