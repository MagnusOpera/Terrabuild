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
    Container: string option
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

type ICache =
    abstract Exists: useRemote:bool -> id:string -> bool
    abstract TryGetSummary: useRemote:bool -> id:string -> TargetSummary option
    abstract CreateEntry: useRemote:bool -> id:string -> IEntry
    abstract CreateHomeDir: nodeHash:string -> string


let private summaryFilename = "summary.json"

let private completeFilename = ".complete"

let private buildCacheDirectory =
    let homeDir = Environment.GetEnvironmentVariable("HOME")
    let cacheDir = IO.combinePath homeDir ".terrabuild/buildcache"
    IO.createDirectory cacheDir
    cacheDir

let private homeDirectory =
    let homeDir = Environment.GetEnvironmentVariable("HOME")
    let cacheDir = IO.combinePath homeDir ".terrabuild/home"
    IO.createDirectory cacheDir
    cacheDir

let private markEntryAsCompleted reason entryDir =
    let completeFile = IO.combinePath entryDir completeFilename
    File.WriteAllText(completeFile, reason)

let clearBuildCache () =
    IO.deleteAny buildCacheDirectory

type NewEntry(entryDir: string, useRemote: bool, id: string, storage: Storages.Storage) =
    let mutable logNum = 0

    let logsDir = IO.combinePath entryDir "logs"
    let outputsDir = IO.combinePath entryDir "outputs"

    do
        match entryDir with
        | IO.Directory _ | IO.File _ -> IO.deleteAny entryDir
        | IO.None -> ()
        IO.createDirectory entryDir
        IO.createDirectory logsDir
        // NOTE: outputs is created on demand only

    let write (summary: TargetSummary) =
        let summary =
            { summary
                with Steps = summary.Steps
                             |> List.map (fun step -> { step
                                                        with Log = IO.getFilename step.Log })
                     Outputs = summary.Outputs
                               |> Option.map (fun outputs -> IO.getFilename outputs) }

        let summaryFile = IO.combinePath logsDir summaryFilename
        summary |> Json.Serialize |> IO.writeTextFile summaryFile

    let upload (storage: Storages.Storage) =
        let uploadDir sourceDir name =
            let tarFile = IO.getTempFilename()
            let compressFile = IO.getTempFilename()
            try
                sourceDir |> Compression.tar tarFile
                tarFile |> Compression.compress compressFile
                storage.Upload $"{id}/{name}" compressFile
            finally
                IO.deleteAny compressFile
                IO.deleteAny tarFile

        if useRemote then
            if Directory.Exists outputsDir then
                uploadDir outputsDir "outputs"
            uploadDir logsDir "logs"

    interface IEntry with
        member _.NextLogFile () =
            logNum <- logNum + 1
            let filename = $"step{logNum}.log"
            IO.combinePath logsDir filename

        member _.Outputs = outputsDir

        member _.Complete summary =
            summary |> write
            storage |> upload
            entryDir |> markEntryAsCompleted "local"


type Cache(storage: Storages.Storage) =
    let cachedSummaries = System.Collections.Concurrent.ConcurrentDictionary<string, TargetSummary>()

    interface ICache with
        member _.Exists useRemote id : bool =
            match cachedSummaries.TryGetValue(id) with
            | true, _ -> true
            | _ ->
                let entryDir = IO.combinePath buildCacheDirectory id
                let completeFile = IO.combinePath entryDir completeFilename


                match completeFile with
                | IO.File _ -> true
                | _ ->
                    if useRemote then storage.Exists $"{id}/logs"
                    else false


        member _.TryGetSummary useRemote id : TargetSummary option =

            match cachedSummaries.TryGetValue(id) with
            | true, summary -> Some summary
            | _ ->
                let entryDir = IO.combinePath buildCacheDirectory id
                let logsDir = IO.combinePath entryDir "logs"
                let outputsDir = IO.combinePath entryDir "outputs"
                let summaryFile = IO.combinePath logsDir summaryFilename
                let completeFile = IO.combinePath entryDir completeFilename

                let load () =
                    let summary  = summaryFile |> IO.readTextFile |> Json.Deserialize<TargetSummary>
                    let summary = { summary
                                    with Steps = summary.Steps
                                                 |> List.map (fun stepLog -> { stepLog
                                                                               with Log = IO.combinePath logsDir stepLog.Log })
                                         Outputs = summary.Outputs |> Option.map (fun _ -> outputsDir) }
                    cachedSummaries.TryAdd(summaryFile, summary) |> ignore
                    summary

                let download (storage: Storages.Storage) =
                    let downloadDir targetDir name =
                        match storage.TryDownload $"{id}/{name}" with
                        | Some tarFile ->
                            let uncompressFile = IO.getTempFilename()
                            try
                                tarFile |> Compression.uncompress uncompressFile
                                uncompressFile |> Compression.untar targetDir
                                Some targetDir
                            finally
                                IO.deleteAny uncompressFile
                                IO.deleteAny tarFile
                        | _ ->
                            None
                    
                    match downloadDir logsDir "logs" with
                    | Some _ ->
                        let summary = load()

                        match summary.Outputs with
                        | Some outputsDir ->
                            match downloadDir outputsDir "outputs" with
                            | Some _ ->
                                entryDir |> markEntryAsCompleted "remote"
                                summary |> Some
                            | _ ->
                                None
                        | _ ->
                            entryDir |> markEntryAsCompleted "remote"
                            summary |> Some
                    | _ ->
                        None

                match completeFile with
                | IO.File _ ->
                    load() |> Some
                | _ ->
                    // cleanup everything - it's not valid anyway
                    IO.deleteAny entryDir

                    if useRemote then storage |> download
                    else None


        member _.CreateEntry useRemote id : IEntry =
            let entryDir = IO.combinePath buildCacheDirectory id
            NewEntry(entryDir, useRemote, id, storage)

        member _.CreateHomeDir nodeHash: string =
            let homeDir = IO.combinePath homeDirectory nodeHash
            homeDir
