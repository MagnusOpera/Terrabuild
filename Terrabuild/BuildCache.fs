module BuildCache
open System
open System.IO
open Helpers

type StepLog = {
    Command: string
    Duration: TimeSpan
    Log: string
}

type Summary = {
    ProjectId: string
    TargetId: string
    StepLogs: StepLog list
    Listing: string
    Dependencies: string list
    Outputs: string
    ExitCode: int
}

let private buildCacheDirectory =
    let homeDir = Environment.GetEnvironmentVariable("HOME")
    let cacheDir = Path.Combine(homeDir, ".terrabuild")
    Directory.CreateDirectory(cacheDir).FullName

let private summaryFilename = "summary.json"


let getBuildSummary (id: string) =
    let entryDir = Path.Combine(buildCacheDirectory, id)
    if Directory.Exists entryDir then
        let summaryFile = Path.Combine(entryDir, summaryFilename)
        match summaryFile with
        | IO.File _ -> 
            let summary  = summaryFile |> IO.readTextFile |> Json.Deserialize<Summary>
            let summary = { summary
                            with StepLogs = summary.StepLogs
                                            |> List.map (fun stepLog -> { stepLog
                                                                          with Log = IO.combine entryDir stepLog.Log })
                                 Outputs = IO.combine entryDir summary.Outputs
                                 Listing = IO.combine entryDir summary.Listing }
            Some summary
        | _ ->
            // cleanup the mess - it's not valid anyway
            Directory.Delete(entryDir, true)
            None
    else
        None

let writeBuildSummary (id: string) (summary: Summary) = 
    let entryDir = Path.Combine(buildCacheDirectory, id)
    if Directory.Exists entryDir then Directory.Delete(entryDir, true)
    Directory.CreateDirectory entryDir |> ignore

    let moveFile idx stepLog =
        let filename = $"step{idx}.log"
        let targetLog = IO.combine entryDir filename
        IO.moveFile stepLog targetLog
        filename

    // move log files to target storage
    let newLogs = summary.StepLogs |> List.mapi (fun idx stepLog -> { stepLog
                                                                      with Log = moveFile idx stepLog.Log })

    let outputsFile = IO.combine entryDir "outputs.zip"
    IO.moveFile summary.Outputs outputsFile

    let listing = IO.combine entryDir "listing.txt"
    IO.writeTextFile listing summary.Listing

    // keep only filename (relative to storage)
    let summary = { summary
                    with StepLogs = newLogs
                         Outputs = "outputs.zip"
                         Listing = "listing.txt" }

    let summaryFile = Path.Combine(entryDir, summaryFilename)
    summary |> Json.Serialize |> IO.writeTextFile summaryFile
