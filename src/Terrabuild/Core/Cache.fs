module Cache
open System
open System.IO

[<RequireQualifiedAccess>]
type TaskStatus =
    | Success
    | Failure

[<RequireQualifiedAccess>]
type StepSummary = {
    MetaCommand: string
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
type Origin =
    | Local
    | Remote

[<RequireQualifiedAccess>]
type TargetSummary = {
    Project: string
    Target: string
    Steps: StepSummary list
    Outputs: string option
    Status: TaskStatus
    StartedAt: DateTime
    EndedAt: DateTime
    Origin: Origin
}


type ArtifactInfo = {
    Path: string
    Size: int
}


type IEntry =
    abstract NextLogFile: unit -> string
    abstract Outputs: string with get
    abstract Complete: summary:TargetSummary -> (string list * int)

type ICache =
    abstract TryGetSummaryOnly: useRemote:bool -> id:string -> TargetSummary option
    abstract TryGetSummary: useRemote:bool -> id:string -> TargetSummary option
    abstract CreateEntry: useRemote:bool -> id:string -> IEntry
    abstract CreateHomeDir: nodeHash:string -> string
    abstract Invalidate: id:string -> unit


let private summaryFilename = "summary.json"

let private completeFilename = "status"

let terrabuildHome =
    FS.combinePath (Environment.GetEnvironmentVariable("HOME")) ".terrabuild"

let private buildCacheDirectory =
    let cacheDir = FS.combinePath terrabuildHome "buildcache"
    IO.createDirectory cacheDir
    cacheDir

let private homeDirectory =
    let cacheDir = FS.combinePath terrabuildHome "home"
    IO.createDirectory cacheDir
    cacheDir

let private setOrigin (origin: Origin) entryDir =
    let originFile = FS.combinePath entryDir completeFilename
    origin |> Json.Serialize |> IO.writeTextFile originFile

let private getOrigin entryDir =
    let originFile = FS.combinePath entryDir completeFilename
    originFile |> IO.readTextFile |> Json.Deserialize<Origin>

let clearBuildCache () =
    IO.deleteAny buildCacheDirectory

let clearHomeCache () =
    IO.deleteAny homeDirectory



type NewEntry(entryDir: string, useRemote: bool, id: string, storage: Contracts.Storage) =
    let mutable logNum = 0

    let logsDir = FS.combinePath entryDir "logs"
    let outputsDir = FS.combinePath entryDir "outputs"

    do
        match entryDir with
        | FS.Directory _ | FS.File _ -> IO.deleteAny entryDir
        | FS.None _ -> ()
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

        let summaryFile = FS.combinePath logsDir summaryFilename
        summary |> Json.Serialize |> IO.writeTextFile summaryFile

    interface IEntry with
        member _.NextLogFile () =
            logNum <- logNum + 1
            let filename = $"step{logNum}.log"
            FS.combinePath logsDir filename

        member _.Outputs = outputsDir

        member _.Complete summary =
            let upload () =
                let uploadDir sourceDir name =
                    let path = $"{id}/{name}"
                    let tarFile = IO.getTempFilename()
                    let compressFile = IO.getTempFilename()
                    try
                        sourceDir |> Compression.tar tarFile
                        tarFile |> Compression.compress compressFile
                        storage.Upload path compressFile
                        path, IO.size compressFile
                    finally
                        IO.deleteAny compressFile
                        IO.deleteAny tarFile

                if useRemote then
                    let fileSizes = [
                        if Directory.Exists outputsDir then uploadDir outputsDir "outputs"
                        uploadDir logsDir "logs"
                    ]
                    fileSizes |> List.fold (fun (files, size) (file, fileSize) -> file :: files, fileSize+size) ([], 0)
                else
                    [], 0

            summary |> write
            let files, size = upload()
            entryDir |> setOrigin summary.Origin
            files, size


type Cache(storage: Contracts.Storage) =
    // if there is a entry we already tried to download the summary (result is the value)
    // if not we have never tried to download the summary
    let cachedSummaries = System.Collections.Concurrent.ConcurrentDictionary<string, TargetSummary option>()

    let tryDownload targetDir id name =
        match storage.TryDownload $"{id}/{name}" with
        | Some file ->
            let uncompressFile = IO.getTempFilename()
            try
                file |> Compression.uncompress uncompressFile
                uncompressFile |> Compression.untar targetDir
                true
            finally
                IO.deleteAny uncompressFile
                IO.deleteAny file
        | _ ->
            false

    let loadSummary logsDir outputsDir summaryFile =
        let summary  = summaryFile |> IO.readTextFile |> Json.Deserialize<TargetSummary>
        let summary = { summary
                        with Steps = summary.Steps
                                        |> List.map (fun stepLog -> { stepLog
                                                                      with Log = FS.combinePath logsDir stepLog.Log })
                             Outputs = summary.Outputs |> Option.map (fun _ -> outputsDir) }
        summary

    interface ICache with
        // NOTE: do not use when building - only use for graph building
        member _.TryGetSummaryOnly useRemote id : TargetSummary option =
            match cachedSummaries.TryGetValue(id) with
            | true, targetSummary ->
                targetSummary
            | false, _ ->
                let entryDir = FS.combinePath buildCacheDirectory id
                let logsDir = FS.combinePath entryDir "logs"
                let outputsDir = FS.combinePath entryDir "outputs"
                let summaryFile = FS.combinePath logsDir summaryFilename
                let completeFile = FS.combinePath entryDir completeFilename

                // do we have the summary in local cache?
                match completeFile with
                | FS.File _ ->
                    let origin = getOrigin entryDir
                    let summary = loadSummary logsDir outputsDir summaryFile
                    let summary = { summary with Origin = origin }
                    cachedSummaries.TryAdd(id, Some summary) |> ignore
                    Some summary
                | _ ->
                    if useRemote then
                        if tryDownload logsDir id "logs" then
                            let summary = loadSummary logsDir outputsDir summaryFile
                            let summary = { summary with Origin = Origin.Remote }
                            cachedSummaries.TryAdd(id, Some summary) |> ignore
                            Some summary
                        else
                            cachedSummaries.TryAdd(id, None) |> ignore
                            None
                    else
                        None

        member _.TryGetSummary useRemote id : TargetSummary option =
            let entryDir = FS.combinePath buildCacheDirectory id
            let logsDir = FS.combinePath entryDir "logs"
            let outputsDir = FS.combinePath entryDir "outputs"
            let summaryFile = FS.combinePath logsDir summaryFilename
            let completeFile = FS.combinePath entryDir completeFilename

            match completeFile with
            | FS.File _ ->
                let origin = getOrigin entryDir
                let summary = loadSummary logsDir outputsDir summaryFile
                let summary = { summary with Origin = origin }
                Some summary
            | _ ->
                if useRemote then
                    if tryDownload logsDir id "logs" then
                        let summary = loadSummary logsDir outputsDir summaryFile
                        let summary = { summary with Origin = Origin.Remote }
                        match summary.Outputs with
                        | Some _ ->
                            if tryDownload outputsDir id "outputs" then
                                entryDir |> setOrigin summary.Origin
                                Some summary
                            else
                                None
                        | _ ->
                            entryDir |> setOrigin summary.Origin
                            Some summary
                    else
                        None
                else
                    None

        member _.Invalidate id =
            cachedSummaries.TryRemove(id) |> ignore
            let entryDir = FS.combinePath buildCacheDirectory id
            entryDir |> IO.deleteAny

        member _.CreateEntry useRemote id : IEntry =
            // invalidate cache as we are creating a new entry
            cachedSummaries.TryRemove(id) |> ignore
            let entryDir = FS.combinePath buildCacheDirectory id
            NewEntry(entryDir, useRemote, id, storage)

        member _.CreateHomeDir nodeHash: string =
            let homeDir = FS.combinePath homeDirectory nodeHash
            homeDir
