module Logs
open Graph
open Cache
open Contracts

let dumpLogs (graph: Workspace) (cache: ICache) (sourceControl: SourceControl) (impactedNodes: string Set option) debug =
    let scope =
        match impactedNodes with
        | Some impactedNodes -> impactedNodes
        | _ -> graph.RootNodes

    graph.Nodes |> Map.iter (fun nodeId node ->

        let dumpLogs (summary: Cache.TargetSummary) =
            summary.Steps |> Seq.iteri (fun index step ->
                if debug then
                    match step.Container with
                    | Some container ->
                        $"{Ansi.Styles.red}{container}: {step.Command} {step.Arguments}{Ansi.Styles.reset}" |> Terminal.writeLine
                    | _ -> ()

                if 0 < index then
                    $"{Ansi.Styles.yellow}+++{Ansi.Styles.reset}" |> Terminal.writeLine
                step.Log |> IO.readTextFile |> Terminal.write
            )

        if scope |> Set.contains nodeId then
            let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.Hash}"
            match cache.TryGetSummary false cacheEntryId with
            | Some summary -> 
                match summary.Status with
                | TaskStatus.Success ->
                    let (logStart, logEnd) = sourceControl.Log true $"{node.Project}/{node.Target}"
                    logStart |> Terminal.writeLine
                    dumpLogs summary
                    logEnd |> Terminal.writeLine
                | TaskStatus.Failure ->
                    let (logStart, logEnd) = sourceControl.Log false $"{node.Project}/{node.Target}"
                    logStart |> Terminal.writeLine
                    dumpLogs summary
                    logEnd |> Terminal.writeLine
            | _ ->
                let (logStart, logEnd) = sourceControl.Log true $"{node.Project}/{node.Target}"
                logStart |> Terminal.writeLine
                $"{Ansi.Styles.yellow}No logs available{Ansi.Styles.reset}" |> Terminal.writeLine
                logEnd |> Terminal.writeLine
    )
