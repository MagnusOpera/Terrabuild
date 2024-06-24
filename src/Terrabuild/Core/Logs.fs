module Logs
open Graph
open Cache
open Contracts




let dumpLogs (graph: Workspace) (cache: ICache) (sourceControl: SourceControl) (impactedNodes: string Set option) debug =
    let scope =
        match impactedNodes with
        | Some impactedNodes -> impactedNodes
        | _ -> graph.Nodes.Keys |> Set

    let dumpMarkdown filename (infos: (Node * TargetSummary option) list) =
        let append line = IO.appendLinesFile filename [line] 

        let dumpMarkdown (node: Node) (summary: TargetSummary option) =

            let title = node.Label

            let header =
                let color =
                    match summary with
                    | Some summary ->
                        match summary.Status with
                        | TaskStatus.Success ->
                            match summary.Origin with
                            | Origin.Local -> "✅"
                            | Origin.Remote -> "🍿"
                        | TaskStatus.Failure ->
                            match summary.Origin with
                            | Origin.Local -> "❌"
                            | Origin.Remote -> "🥨"
                    | _ -> "❓"

                $"## <a name=\"{node.Id}\"></a> {color} {title}"

            let dumpLogs =
                match summary with
                | Some summary -> 
                    let dumpLogs () =

                        let batchNode =
                            match node.Batched, node.Dependencies |> Seq.tryHead with
                            | true, Some batchId -> Some graph.Nodes[batchId]
                            | _ -> None

                        match batchNode with
                        | Some batchNode -> $"**Batched with [{batchNode.Label}](#{batchNode.Id})**" |> append
                        | _ ->
                            summary.Steps |> Seq.iter (fun step ->
                                $"### {step.MetaCommand}" |> append
                                if debug then
                                    let cmd = $"{step.Command} {step.Arguments}" |> String.trim
                                    "<detail><summary>command</summary>" |> append
                                    $"*{cmd}*" |> append
                                    "</detail>" |> append

                                append "```"
                                step.Log |> IO.readTextFile |> append
                                append "```"
                        )

                    match summary.Status with
                    | TaskStatus.Success -> dumpLogs
                    | TaskStatus.Failure -> dumpLogs
                | _ ->
                    let dumpNoLog() = $"**No logs available**" |> append
                    dumpNoLog

            header |> append
            dumpLogs ()


        "# Summary" |> append

        "# Details" |> append
        infos |> List.iter (fun (node, summary) -> dumpMarkdown node summary)


    let dumpTerminal (infos: (Node * TargetSummary option) seq) =
        let dumpTerminal (node: Node) (summary: TargetSummary option) =
            let title = node.Label

            let getHeaderFooter success title =
                let color =
                    if success then $"{Ansi.Styles.green}{Ansi.Emojis.checkmark}"
                    else $"{Ansi.Styles.red}{Ansi.Emojis.crossmark}"

                $"{color} {title}{Ansi.Styles.reset}", ""

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
                                    $"{Ansi.Styles.cyan}{step.Command} {step.Arguments}{Ansi.Styles.reset}" |> Terminal.writeLine
                                step.Log |> IO.readTextFile |> Terminal.write
                        )

                    match summary.Status with
                    | TaskStatus.Success -> getHeaderFooter true title, dumpLogs
                    | TaskStatus.Failure -> getHeaderFooter false title, dumpLogs
                | _ ->
                    let dumpNoLog() = $"{Ansi.Styles.yellow}No logs available{Ansi.Styles.reset}" |> Terminal.writeLine
                    getHeaderFooter false title, dumpNoLog

            logStart |> Terminal.writeLine
            dumpLogs ()
            logEnd |> Terminal.writeLine

        infos |> Seq.iter (fun (node, summary) -> dumpTerminal node summary)

    let logger =
        match sourceControl.LogType with
        | Terminal -> dumpTerminal
        | Markdown filename -> dumpMarkdown filename

    // filter, collect summaries and dump
    graph.Nodes
    |> Seq.choose (fun (KeyValue(nodeId, node)) -> if scope |> Set.contains nodeId then Some node else None)
    |> Seq.map (fun node ->
        let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.Hash}"
        let summary = cache.TryGetSummaryOnly false cacheEntryId
        node, summary)
    |> Seq.sortBy (fun (_, summary) -> summary |> Option.map (fun summary -> summary.EndedAt))
    |> List.ofSeq
    |> logger
