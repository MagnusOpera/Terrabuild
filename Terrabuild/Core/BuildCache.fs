module BuildCache
open System
open System.IO

type StepInfo = {
    Command: string
    Arguments: string
    StartedAt: DateTime
    EndedAt: DateTime
    Duration: TimeSpan
    Log: string
}

type Summary = {
    Project: string
    Target: string
    Steps: StepInfo list
    Files: Set<string>    
    Ignores: Set<string>
    Variables: Map<string, string>
    Outputs: string option
    ExitCode: int
}


type IEntry =
    abstract NextLogFile: unit -> string
    abstract Outputs: string with get
    abstract Complete: summary:Summary -> unit


let private summaryFilename = "summary.json"

let private completeFilename = ".complete"

let private buildCacheDirectory =
    let homeDir = Environment.GetEnvironmentVariable("HOME")
    let cacheDir = Path.Combine(homeDir, ".terrabuild")
    Directory.CreateDirectory(cacheDir).FullName

let private markEntryAsCompleted entryDir =
    let completeFile = IO.combine entryDir completeFilename
    File.WriteAllText(completeFile, "")


type NewEntry(entryDir: string, id: string, storage: Storages.Storage option) =
    let mutable logNum = 0

    do
        match entryDir with
        | IO.Directory _ -> Directory.Delete(entryDir, true)
        | IO.None -> ()
        | _ -> failwith $"Unexpected file at '{entryDir}'"
        Directory.CreateDirectory(entryDir) |> ignore

    interface IEntry with
        member _.NextLogFile () =
            logNum <- logNum + 1
            let filename = $"step{logNum}.log"
            IO.combine entryDir filename

        member _.Outputs = IO.combine entryDir "outputs"

        member _.Complete summary =
            let summary =
                { summary
                  with Steps = summary.Steps
                               |> List.map (fun step -> { step
                                                          with Log = IO.getFilename step.Log })
                       Outputs = summary.Outputs
                                 |> Option.map (fun outputs -> IO.getFilename outputs) }

            let summaryFile = Path.Combine(entryDir, summaryFilename)
            summary |> Json.Serialize |> IO.writeTextFile summaryFile

            match storage with
            | Some storage ->
                let files = IO.enumerateFiles entryDir
                let tmpArchive = Zip.createArchive entryDir files
                storage.Upload id tmpArchive
            | _ -> ()

            entryDir |> markEntryAsCompleted


type Cache(storage: Storages.Storage option) =
    member _.TryGetSummary id : Summary option =
        let entryDir = Path.Combine(buildCacheDirectory, id)
        
        let loadSummary () =
            let summaryFile = Path.Combine(entryDir, summaryFilename)
            let summary  = summaryFile |> IO.readTextFile |> Json.Deserialize<Summary>
            let summary = { summary
                            with Steps = summary.Steps
                                         |> List.map (fun stepLog -> { stepLog
                                                                       with Log = IO.combine entryDir stepLog.Log })
                                 Outputs = summary.Outputs |> Option.map (fun outputs -> IO.combine entryDir outputs) }
            summary

        let completeFile = IO.combine entryDir completeFilename
        match completeFile with
        | IO.File _ -> loadSummary() |> Some
        | _ ->
            // cleanup the mess - it's not valid anyway
            if Directory.Exists entryDir then Directory.Delete(entryDir, true)

            // try get remote entry
            match storage with
            | Some storage ->
                match storage.TryDownload id with
                | Some tmpArchive ->
                    Zip.restoreArchive tmpArchive entryDir
                    let summary = loadSummary()
                    entryDir |> markEntryAsCompleted
                    summary |> Some
                | _ -> None
            | _ -> None

    member _.CreateEntry id : IEntry =
        let entryDir = IO.combine buildCacheDirectory id
        NewEntry(entryDir, id, storage)
