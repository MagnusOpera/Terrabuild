module Logs
open Graph
open Cache
open Contracts

let dumpLogs (graph: Workspace) (cache: ICache) (sourceControl: SourceControl) (impactedNodes: string Set option) debug =
    let scope =
        match impactedNodes with
        | Some impactedNodes -> impactedNodes
        | _ -> graph.Nodes.Keys |> Set

    // filter, collect summaries and dump
    graph.Nodes
    |> Seq.choose (fun (KeyValue(nodeId, node)) -> if scope |> Set.contains nodeId then Some node else None)
    |> Seq.map (fun node ->
        let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.Hash}"
        let summary = cache.TryGetSummary false cacheEntryId
        node, summary)
    |> Seq.sortBy (fun (_, summary) -> summary |> Option.map (fun summary -> summary.EndedAt))
    |> Seq.iter (fun (node, summary) ->
        let title = node.Label

        let (logStart, logEnd), dumpLogs =
            match summary with
            | Some summary -> 
                let dumpLogs () =

                    let batchNode =
                        match node.Batched, node.Dependencies |> Seq.tryHead with
                        | true, Some batchId -> Some graph.Nodes[batchId]
                        | _ -> None

                    match batchNode with
                    | Some batchNode -> $"{Ansi.Styles.yellow}Batched with '{batchNode.Label}'{Ansi.Styles.reset}" |> Terminal.writeLine
                    | _ ->
                        summary.Steps |> Seq.iter (fun step ->
                            $"{Ansi.Styles.yellow}{step.MetaCommand}{Ansi.Styles.reset}" |> Terminal.writeLine
                            if debug then
                                match step.Container with
                                | Some container ->
                                    $"{Ansi.Styles.cyan}{container} [{step.Command} {step.Arguments}]{Ansi.Styles.reset}" |> Terminal.writeLine
                                | _ -> ()

                            step.Log |> IO.readTextFile |> Terminal.write
                    )

                match summary.Status with
                | TaskStatus.Success -> sourceControl.Log true title, dumpLogs
                | TaskStatus.Failure -> sourceControl.Log false title, dumpLogs
            | _ ->
                let dumpNoLog() = $"{Ansi.Styles.yellow}No logs available{Ansi.Styles.reset}" |> Terminal.writeLine
                sourceControl.Log true title, dumpNoLog

        logStart |> Terminal.writeLine
        dumpLogs ()
        logEnd |> Terminal.writeLine
    )
