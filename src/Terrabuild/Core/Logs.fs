module Logs
open Graph
open Cache
open Contracts

let dumpLogs (graph: Workspace) (cache: ICache) (sourceControl: SourceControl)=
    graph.RootNodes |> Seq.iter (fun depId ->

        let dumpLogs (summary: Cache.TargetSummary) =
            summary.Steps |> Seq.iteri (fun index step ->
                if 0 < index then
                    $"{Ansi.Styles.yellow}────────────────────────────────────────────────────────────────────────────────{Ansi.Styles.reset}" |> Terminal.writeLine
                step.Log |> IO.readTextFile |> Terminal.write
            )

        let node = graph.Nodes[depId]
        let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.Hash}"
        match cache.TryGetSummary false cacheEntryId with
        | Some summary -> 
            match summary.Status with
            | TaskStatus.Success ->
                let (logStart, logEnd) = sourceControl.Log true $"{summary.Project}/{summary.Target}"
                logStart |> Terminal.writeLine
                dumpLogs summary
                logEnd |> Terminal.writeLine
            | TaskStatus.Failure ->
                let (logStart, logEnd) = sourceControl.Log false $"{summary.Project}/{summary.Target}"
                logStart |> Terminal.writeLine
                dumpLogs summary
                logEnd |> Terminal.writeLine
        | _ -> ()
    )
