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



[<RequireQualifiedAccess>]
type Configuration = {
    Token: string option
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
    abstract Exists: useRemote:bool -> id:string -> bool
    abstract TryGetSummary: useRemote:bool -> id:string -> TargetSummary option
    abstract CreateEntry: useRemote:bool -> id:string -> IEntry
    abstract CreateHomeDir: nodeHash:string -> string


let private summaryFilename = "summary.json"

let private completeFilename = ".complete"

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

let private markEntryAsCompleted reason entryDir =
    let completeFile = FS.combinePath entryDir completeFilename
    File.WriteAllText(completeFile, reason)

let clearBuildCache () =
    IO.deleteAny buildCacheDirectory


let removeAuthToken () =
    let configFile = FS.combinePath terrabuildHome "config.json"
    let config =
        if File.Exists configFile then configFile |> IO.readTextFile |> Json.Deserialize<Configuration>
        else { Configuration.Token = None }

    let config = { config with Token = None }
    config
    |> Json.Serialize
    |> IO.writeTextFile configFile

let addAuthToken (token: string) =
    let configFile = FS.combinePath terrabuildHome "config.json"
    let config =
        if File.Exists configFile then configFile |> IO.readTextFile |> Json.Deserialize<Configuration>
        else { Token = None }

    let config = { config with Token = Some token }

    config
    |> Json.Serialize
    |> IO.writeTextFile configFile

let readAuthToken () =
    let configFile = FS.combinePath terrabuildHome "config.json"
    let config =
        if File.Exists configFile then configFile |> IO.readTextFile |> Json.Deserialize<Configuration>
        else { Configuration.Token = None }

    config.Token

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
            entryDir |> markEntryAsCompleted "local"
            files, size


type Cache(storage: Contracts.Storage) =
    let cachedExists = System.Collections.Concurrent.ConcurrentDictionary<string, bool>()
    let cachedSummaries = System.Collections.Concurrent.ConcurrentDictionary<string, TargetSummary>()

    interface ICache with
        member _.Exists useRemote id : bool =
            match cachedExists.TryGetValue(id) with
            | true, _ ->
                true
            | _ ->
                match cachedSummaries.TryGetValue(id) with
                | true, _ ->
                    cachedExists.TryAdd(id, true) |> ignore
                    true
                | _ ->
                    let entryDir = FS.combinePath buildCacheDirectory id
                    let completeFile = FS.combinePath entryDir completeFilename

                    match completeFile with
                    | FS.File _ ->
                        cachedExists.TryAdd(id, true) |> ignore
                        true
                    | _ ->
                        // NOTE: probably overcaching here but since Exists is not used after TryGetSummary it's ok
                        let res =
                            if useRemote then storage.Exists $"{id}/logs"
                            else false
                        cachedExists.TryAdd(id, res) |> ignore
                        res

        member _.TryGetSummary useRemote id : TargetSummary option =

            match cachedSummaries.TryGetValue(id) with
            | true, summary -> Some summary
            | _ ->
                let entryDir = FS.combinePath buildCacheDirectory id
                let logsDir = FS.combinePath entryDir "logs"
                let outputsDir = FS.combinePath entryDir "outputs"
                let summaryFile = FS.combinePath logsDir summaryFilename
                let completeFile = FS.combinePath entryDir completeFilename

                let load () =
                    let summary  = summaryFile |> IO.readTextFile |> Json.Deserialize<TargetSummary>
                    let summary = { summary
                                    with Steps = summary.Steps
                                                 |> List.map (fun stepLog -> { stepLog
                                                                               with Log = FS.combinePath logsDir stepLog.Log })
                                         Outputs = summary.Outputs |> Option.map (fun _ -> outputsDir) }
                    cachedSummaries.TryAdd(summaryFile, summary) |> ignore
                    summary

                let download (storage: Contracts.Storage) =
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
                | FS.File _ ->
                    load() |> Some
                | _ ->
                    // cleanup everything - it's not valid anyway
                    IO.deleteAny entryDir

                    if useRemote then storage |> download
                    else None


        member _.CreateEntry useRemote id : IEntry =
            let entryDir = FS.combinePath buildCacheDirectory id
            NewEntry(entryDir, useRemote, id, storage)

        member _.CreateHomeDir nodeHash: string =
            let homeDir = FS.combinePath homeDirectory nodeHash
            homeDir
