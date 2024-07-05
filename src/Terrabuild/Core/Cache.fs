module Cache
open System
open System.IO
open Collections

[<RequireQualifiedAccess>]
type Origin =
    | Local
    | Remote

[<RequireQualifiedAccess>]
type OperationSummary = {
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
type TargetSummary = {
    Project: string
    Target: string
    Operations: OperationSummary list list
    Outputs: string option
    IsSuccessful: bool
    StartedAt: DateTime
    EndedAt: DateTime
}


type ArtifactInfo = {
    Path: string
    Size: int
}


type IEntry =
    abstract NextLogFile: unit -> string
    abstract CompleteLogFile: summary:TargetSummary -> unit
    abstract Outputs: string with get
    abstract Complete: unit -> string list * int

type ICache =
    abstract TryGetSummaryOnly: useRemote:bool -> id:string -> (Origin * TargetSummary) option
    abstract TryGetSummary: useRemote:bool -> id:string -> TargetSummary option
    abstract GetEntry: useRemote:bool -> clean:bool -> id:string -> IEntry
    abstract CreateHomeDir: nodeHash:string -> string
    abstract Invalidate: id:string -> unit


let private summaryFilename = "summary.json"

let private originFilename = "origin"

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
    let originFile = FS.combinePath entryDir originFilename
    origin |> Json.Serialize |> IO.writeTextFile originFile

let private getOrigin entryDir =
    let originFile = FS.combinePath entryDir originFilename
    originFile |> IO.readTextFile |> Json.Deserialize<Origin>

let clearBuildCache () =
    IO.deleteAny buildCacheDirectory

let clearHomeCache () =
    IO.deleteAny homeDirectory



type NewEntry(entryDir: string, useRemote: bool, clean: bool, id: string, storage: Contracts.IStorage) =
    let logsDir = FS.combinePath entryDir "logs"
    let outputsDir = FS.combinePath entryDir "outputs"
    let mutable logNum = 1

    do
        if clean then
            match entryDir with
            | FS.Directory _ | FS.File _ -> IO.deleteAny entryDir
            | FS.None _ -> ()

        IO.createDirectory entryDir
        IO.createDirectory logsDir
        // NOTE: outputs is created on demand only

    let write (summary: TargetSummary) file =
        let summary =
            { summary
                with Operations = summary.Operations
                             |> List.map (fun stepGroup ->
                                stepGroup
                                |> List.map (fun step -> { step
                                                            with Log = IO.getFilename step.Log }))
                     Outputs = summary.Outputs
                               |> Option.map (fun outputs -> IO.getFilename outputs) }

        summary |> Json.Serialize |> IO.writeTextFile file

    interface IEntry with

        member _.NextLogFile () =
            let rec nextLogFile() =
                let filename = FS.combinePath logsDir $"step{logNum}.log"
                if IO.exists filename then
                    logNum <- logNum + 1
                    nextLogFile()
                else
                    filename
            nextLogFile()

        member _.CompleteLogFile summary =
            FS.combinePath logsDir $"step{logNum}.json" |> write summary

        member _.Outputs = outputsDir

        member _.Complete() =
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

            let rec collect logNum =
                seq {
                    let filename = FS.combinePath logsDir $"step{logNum}.json"
                    if IO.exists filename then
                        let json = IO.readTextFile filename
                        json |> Json.Deserialize<TargetSummary>
                        yield! collect (logNum+1)
                }

            let summary =
                collect 1
                |> Seq.reduce (fun s1 s2 -> { s1 with Operations = s1.Operations @ s2.Operations; EndedAt = s2.EndedAt })

            let summaryfile = FS.combinePath logsDir "summary.json"
            write summary summaryfile

            let files, size = upload()
            entryDir |> setOrigin Origin.Local
            files, size


type Cache(storage: Contracts.IStorage) =
    // if there is a entry we already tried to download the summary (result is the value)
    // if not we have never tried to download the summary
    let cachedSummaries = System.Collections.Concurrent.ConcurrentDictionary<string, (Origin*TargetSummary) option>()

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
                        with Operations = summary.Operations
                                     |> List.map (fun stepGroup ->
                                        stepGroup
                                        |> List.map (fun stepLog -> { stepLog
                                                                      with Log = FS.combinePath logsDir stepLog.Log }))
                             Outputs = summary.Outputs |> Option.map (fun _ -> outputsDir) }
        summary

    interface ICache with
        // NOTE: do not use when building - only use for graph building
        member _.TryGetSummaryOnly useRemote id : (Origin * TargetSummary) option =
            match cachedSummaries.TryGetValue(id) with
            | true, originSummary -> originSummary
            | false, _ ->
                let entryDir = FS.combinePath buildCacheDirectory id
                let logsDir = FS.combinePath entryDir "logs"
                let outputsDir = FS.combinePath entryDir "outputs"
                let summaryFile = FS.combinePath logsDir summaryFilename
                let completeFile = FS.combinePath entryDir originFilename

                // do we have the summary in local cache?
                match completeFile with
                | FS.File _ ->
                    let summary = loadSummary logsDir outputsDir summaryFile
                    let origin = getOrigin entryDir
                    cachedSummaries.TryAdd(id, Some (origin, summary)) |> ignore
                    Some (origin, summary)
                | _ ->
                    if useRemote then
                        if tryDownload logsDir id "logs" then
                            let summary = loadSummary logsDir outputsDir summaryFile
                            cachedSummaries.TryAdd(id, Some (Origin.Remote, summary)) |> ignore
                            Some (Origin.Remote, summary)
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
            let completeFile = FS.combinePath entryDir originFilename

            match completeFile with
            | FS.File _ ->
                let summary = loadSummary logsDir outputsDir summaryFile
                Some summary
            | _ ->
                if useRemote then
                    if tryDownload logsDir id "logs" then
                        let summary = loadSummary logsDir outputsDir summaryFile
                        match summary.Outputs with
                        | Some _ ->
                            if tryDownload outputsDir id "outputs" then
                                entryDir |> setOrigin Origin.Remote
                                Some summary
                            else
                                None
                        | _ ->
                            entryDir |> setOrigin Origin.Remote
                            Some summary
                    else
                        None
                else
                    None

        member _.Invalidate id =
            cachedSummaries.TryRemove(id) |> ignore
            let entryDir = FS.combinePath buildCacheDirectory id
            entryDir |> IO.deleteAny

        member _.GetEntry useRemote clean id : IEntry =
            // invalidate cache as we are creating a new entry
            cachedSummaries.TryRemove(id) |> ignore
            let entryDir = FS.combinePath buildCacheDirectory id
            NewEntry(entryDir, useRemote, clean, id, storage)

        member _.CreateHomeDir nodeHash: string =
            let homeDir = FS.combinePath homeDirectory nodeHash
            homeDir
