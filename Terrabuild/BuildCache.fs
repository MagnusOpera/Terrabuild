module BuildCache
open System
open System.IO
open Helpers

type Summary = {
    ProjectId: string
    TargetId: string
    StepLogs: List<string>
    Listing: string
    Dependencies: List<string>
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
                            with StepLogs = summary.StepLogs |> List.map (IO.combine entryDir)
                                 Outputs = IO.combine entryDir summary.Outputs }
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
    let newLogs = summary.StepLogs |> List.mapi moveFile 

    let outputsFile = IO.combine entryDir "outputs.zip"
    IO.moveFile summary.Outputs outputsFile

    // keep only filename (relative to storage)
    let summary = { summary
                    with StepLogs = newLogs
                         Outputs = "outputs.zip" }

    let summaryFile = Path.Combine(entryDir, summaryFilename)
    summary |> Json.Serialize |> IO.writeTextFile summaryFile
