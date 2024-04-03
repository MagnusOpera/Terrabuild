module FileSystem
open System

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

let createSnapshot projectDirectory outputs =
    let files =
        IO.enumerateFilesMatch outputs projectDirectory
        |> Seq.map (fun output -> output, System.IO.File.GetLastWriteTimeUtc output)
        |> Map
    { TimestampedFiles = files }
