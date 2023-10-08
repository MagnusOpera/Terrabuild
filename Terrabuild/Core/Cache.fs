module Cache
open System
open System.IO
open System.Formats.Tar
open System.IO.Compression

[<RequireQualifiedAccess>]
type TaskStatus =
    | Success
    | Failure

[<RequireQualifiedAccess>]
type StepSummary = {
    Command: string
    Arguments: string
    StartedAt: DateTime
    EndedAt: DateTime
    Duration: TimeSpan
    Log: string
    ExitCode: int
}

[<RequireQualifiedAccess>]
type TargetSummary = {
    Project: string
    Target: string
    Steps: StepSummary list
    Files: Set<string>    
    Ignores: Set<string>
    Variables: Map<string, string>
    Outputs: string option
    Status: TaskStatus
}


type IEntry =
    abstract NextLogFile: unit -> string
    abstract Outputs: string with get
    abstract Complete: summary:TargetSummary -> unit


let private summaryFilename = "summary.json"

let private completeFilename = ".complete"

let private buildCacheDirectory =
    let homeDir = Environment.GetEnvironmentVariable("HOME")
    let cacheDir = IO.combinePath homeDir ".terrabuild/buildcache"
    IO.createDirectory cacheDir
    cacheDir

let private markEntryAsCompleted entryDir =
    let completeFile = IO.combinePath entryDir completeFilename
    File.WriteAllText(completeFile, "")

let clearBuildCache () =
    IO.deleteAny buildCacheDirectory

type NewEntry(entryDir: string, id: string, storage: Storages.Storage option) =
    let mutable logNum = 0

    do
        match entryDir with
        | IO.Directory _ | IO.File _ -> IO.deleteAny entryDir
        | IO.None -> ()
        IO.createDirectory entryDir

    interface IEntry with
        member _.NextLogFile () =
            logNum <- logNum + 1
            let filename = $"step{logNum}.log"
            IO.combinePath entryDir filename

        member _.Outputs = IO.combinePath entryDir "outputs"

        member _.Complete summary =
            let summary =
                { summary
                  with Steps = summary.Steps
                               |> List.map (fun step -> { step
                                                          with Log = IO.getFilename step.Log })
                       Outputs = summary.Outputs
                                 |> Option.map (fun outputs -> IO.getFilename outputs) }

            let summaryFile = IO.combinePath entryDir summaryFilename
            summary |> Json.Serialize |> IO.writeTextFile summaryFile

            match storage with
            | Some storage ->
                let tarFile = IO.getTempFilename()
                let compressFile = IO.getTempFilename()
                try
                    entryDir |> Compression.tar tarFile
                    tarFile |> Compression.compress compressFile
                    storage.Upload id compressFile
                finally
                    IO.deleteAny compressFile
                    IO.deleteAny tarFile

            | _ -> ()

            entryDir |> markEntryAsCompleted


type Cache(storage: Storages.Storage option) =
    member _.TryGetSummary id : TargetSummary option =
        let entryDir = IO.combinePath buildCacheDirectory id
        
        let loadSummary () =
            let summaryFile = IO.combinePath entryDir summaryFilename
            let summary  = summaryFile |> IO.readTextFile |> Json.Deserialize<TargetSummary>
            let summary = { summary
                            with Steps = summary.Steps
                                         |> List.map (fun stepLog -> { stepLog
                                                                       with Log = IO.combinePath entryDir stepLog.Log })
                                 Outputs = summary.Outputs |> Option.map (fun outputs -> IO.combinePath entryDir outputs) }
            summary

        let completeFile = IO.combinePath entryDir completeFilename
        match completeFile with
        | IO.File _ ->
            loadSummary() |> Some
        | _ ->
            // cleanup the mess - it's not valid anyway
            IO.deleteAny entryDir

            // try get remote entry
            match storage with
            | Some storage ->
                match storage.TryDownload id with
                | Some tarFile ->
                    let uncompressFile = IO.getTempFilename()
                    try
                        tarFile |> Compression.uncompress uncompressFile
                        uncompressFile |> Compression.untar entryDir
                        let summary = loadSummary()
                        entryDir |> markEntryAsCompleted
                        summary |> Some
                    finally
                        IO.deleteAny uncompressFile
                        IO.deleteAny tarFile
                | _ ->
                    None
            | _ -> None

    member _.CreateEntry id : IEntry =
        let entryDir = IO.combinePath buildCacheDirectory id
        NewEntry(entryDir, id, storage)
