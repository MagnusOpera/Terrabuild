module FileSystem
open System

type Snapshot = {
    TimestampedFiles: Map<string, DateTime>
}
with
    static member (-) (afterOutputs: Snapshot, beforeOutputs: Snapshot) =
        let newOutputs =
            afterOutputs.TimestampedFiles
            |> Seq.choose (fun afterOutput ->
                match beforeOutputs.TimestampedFiles |> Map.tryFind afterOutput.Key with
                | Some prevWriteDate when afterOutput.Value = prevWriteDate -> None
                | _ -> Some afterOutput.Key)
            |> List.ofSeq
        newOutputs

let createSnapshot projectDirectory outputs =
    let files =
        outputs
        |> Seq.map (IO.combinePath projectDirectory)
        |> Seq.collect (fun output ->
            match output with
            | IO.File _ -> [ output, System.IO.File.GetLastWriteTimeUtc output ]
            | IO.Directory _ -> System.IO.Directory.EnumerateFiles(output, "*", System.IO.SearchOption.AllDirectories)
                                |> Seq.map (fun file -> file, System.IO.File.GetLastWriteTimeUtc file)
                                |> List.ofSeq
            | _ -> [])
        |> Map.ofSeq
    { TimestampedFiles = files }
