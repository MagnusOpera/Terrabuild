module Cache
open System
open System.IO

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

    let write (summary: TargetSummary) =
        let summary =
            { summary
                with Steps = summary.Steps
                             |> List.map (fun step -> { step
                                                        with Log = IO.getFilename step.Log })
                     Outputs = summary.Outputs
                               |> Option.map (fun outputs -> IO.getFilename outputs) }

        let summaryFile = IO.combinePath entryDir summaryFilename
        summary |> Json.Serialize |> IO.writeTextFile summaryFile

    let upload (storage: Storages.Storage) =
        let tarFile = IO.getTempFilename()
        let compressFile = IO.getTempFilename()
        try
            entryDir |> Compression.tar tarFile
            tarFile |> Compression.compress compressFile
            storage.Upload id compressFile
        finally
            IO.deleteAny compressFile
            IO.deleteAny tarFile

    interface IEntry with
        member _.NextLogFile () =
            logNum <- logNum + 1
            let filename = $"step{logNum}.log"
            IO.combinePath entryDir filename

        member _.Outputs = IO.combinePath entryDir "outputs"

        member _.Complete summary =
            summary |> write
            storage |> Option.iter upload
            entryDir |> markEntryAsCompleted


type Cache(storage: Storages.Storage option) =
    let cachedSumaries = System.Collections.Concurrent.ConcurrentDictionary<string, TargetSummary>()

    member _.TryGetSummary id : TargetSummary option =

        match cachedSumaries.TryGetValue(id) with
        | true, summary -> Some summary
        | _ ->
            let entryDir = IO.combinePath buildCacheDirectory id
            let summaryFile = IO.combinePath entryDir summaryFilename
            let completeFile = IO.combinePath entryDir completeFilename

            let load () =
                let summary  = summaryFile |> IO.readTextFile |> Json.Deserialize<TargetSummary>
                let summary = { summary
                                with Steps = summary.Steps
                                             |> List.map (fun stepLog -> { stepLog
                                                                           with Log = IO.combinePath entryDir stepLog.Log })
                                     Outputs = summary.Outputs |> Option.map (fun outputs -> IO.combinePath entryDir outputs) }
                cachedSumaries.TryAdd(summaryFile, summary) |> ignore
                summary

            let download (storage: Storages.Storage) =
                match storage.TryDownload id with
                | Some tarFile ->
                    let uncompressFile = IO.getTempFilename()
                    try
                        tarFile |> Compression.uncompress uncompressFile
                        uncompressFile |> Compression.untar entryDir
                        entryDir |> markEntryAsCompleted
                        let summary = load()
                        summary |> Some
                    finally
                        IO.deleteAny uncompressFile
                        IO.deleteAny tarFile
                | _ ->
                    None


            match completeFile with
            | IO.File _ ->
                load() |> Some
            | _ ->
                // cleanup everything - it's not valid anyway
                IO.deleteAny entryDir
                storage |> Option.bind download


    member _.CreateEntry id : IEntry =
        let entryDir = IO.combinePath buildCacheDirectory id
        NewEntry(entryDir, id, storage)
