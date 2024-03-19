#if !TERRABUILD_SCRIPT
#r "../../Terrabuild/bin/Debug/net8.0/Terrabuild.Extensibility.dll"
#endif

open Terrabuild.Extensibility
open System.IO
open System.Text.RegularExpressions

let private (|Regex|_|) pattern input =
    let m = Regex.Match(input, pattern)
    if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
    else None

let __init__ (context: InitContext) =
    let dependencies =
        Path.Combine(context.Directory, "*.sln")
        |> Directory.EnumerateFiles |> Seq.head
        |> File.ReadLines
        |> Seq.choose (fun line ->
            match line with
            | Regex "Project\(.*\) = \".*\", \"(.*)\", .*" [projectFile] -> Some projectFile
            | _ -> None)
        |> Set.ofSeq

    { ProjectInfo.Default
      with Ignores = Set.empty
           Outputs = Set.empty
           Dependencies = set dependencies }
