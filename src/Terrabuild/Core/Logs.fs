module Logs
open Graph
open Cache
open Contracts
open System




let dumpLogs (logId: Guid) (graph: Workspace) (cache: ICache) (sourceControl: SourceControl) (impactedNodes: string Set option) debug =
    let stableRandomId (id: string) =
        $"{logId} {id}" |> Hash.md5 |> String.toLower

    let scope =
        match impactedNodes with
        | Some impactedNodes -> impactedNodes
        | _ -> graph.Nodes.Keys |> Set

    // filter, collect summaries and dump
    let nodes =
        graph.Nodes
        |> Seq.choose (fun (KeyValue(nodeId, node)) -> if scope |> Set.contains nodeId then Some node else None)
        |> Seq.map (fun node ->
            let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.Hash}"
            let summary = cache.TryGetSummaryOnly false cacheEntryId
            node, summary)
        |> Seq.sortBy (fun (_, summary) -> summary |> Option.map (fun summary -> summary.EndedAt))
        |> List.ofSeq

    let successful =
        nodes
        |> List.forall (fun (node, targetSummary) ->
            match targetSummary with
            | Some summary -> summary.Status = TaskStatus.Success
            | _ -> false)

    let dumpMarkdown filename (infos: (Node * TargetSummary option) list) =
        let appendLines lines = IO.appendLinesFile filename lines 
        let append line = appendLines [line]

        let statusEmoji (summary: TargetSummary option) =
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


        let dumpMarkdown (node: Node) (summary: TargetSummary option) =
            let header =
                let statusEmoji = statusEmoji summary
                let uniqueId = stableRandomId node.Id
                $"## <a name=\"user-content-{uniqueId}\"></a> {statusEmoji} {node.Label}"

            let dumpLogs =
                match summary with
                | Some summary -> 
                    let dumpLogs () =

                        let batchNode =
                            match node.Batched, node.Dependencies |> Seq.tryHead with
                            | true, Some batchId -> Some graph.Nodes[batchId]
                            | _ -> None

                        match batchNode with
                        | Some batchNode ->
                            let uniqueId = stableRandomId batchNode.Id
                            $"**Batched with [{batchNode.Label}](#user-content-{uniqueId})**" |> append
                        | _ ->
                            summary.Steps |> List.iter (fun step ->
                                $"### {step.MetaCommand}" |> append
                                if debug then
                                    let cmd = $"{step.Command} {step.Arguments}" |> String.trim
                                    $"*{cmd}*" |> append

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

        let targets = graph.Targets |> String.join " "
        let message, color =
            if successful then "success", "success"
            else "failure", "critical"
        let targetsBadge = graph.Targets |> String.join "_"
        let summaryAnchor = stableRandomId "summary"
        $"[![{targets}](https://img.shields.io/badge/{targetsBadge}-build_{message}-{color})](#user-content-{summaryAnchor})" |> append

        $"<details><summary>Expand for details</summary>" |> append

        "" |> append
        $"# <a name=\"user-content-{summaryAnchor}\"></a> Summary" |> append
        "| Target | Duration |" |> append
        "|--------|----------|" |> append
        infos |> List.iter (fun (node, summary) ->
            let statusEmoji = statusEmoji summary
            let duration =
                match summary with
                | Some summary -> $"{summary.EndedAt - summary.StartedAt}"
                | _ -> ""

            let uniqueId = stableRandomId node.Id
            $"| {statusEmoji} [{node.Label}](#user-content-{uniqueId}) | {duration} |" |> append
        )
        let (cost, gain) =
            infos |> List.fold (fun (cost, gain) (_, summary) ->
                match summary with
                | Some summary ->
                    let duration = summary.EndedAt - summary.StartedAt
                    if summary.Origin = Origin.Local then cost + duration, gain
                    else cost, gain + duration
                | _ -> cost, gain
            ) (TimeSpan.Zero, TimeSpan.Zero)
        $"| Cost | {cost} |" |> append
        $"| Gain | {gain} |" |> append

        "" |> append
        let mermaid = Graph.graph graph
        $"# Build Graph" |> append
        "```mermaid" |> append
        mermaid |> appendLines
        "```" |> append

        "" |> append
        "# Details" |> append
        infos |> List.iter (fun (node, summary) -> dumpMarkdown node summary)
        "" |> append

        "</details>" |> append

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

    let reportFailedNodes (infos: (Node * TargetSummary option) list) =
        infos
        |> List.iter (fun (node, summary) ->
            match summary with
            | Some summary when summary.Status = TaskStatus.Failure -> sourceControl.LogError $"{node.Label} failed"
            | _ -> ())

    let logger =
        match sourceControl.LogType with
        | Terminal -> dumpTerminal
        | Markdown filename -> dumpMarkdown filename

    nodes |> logger
    nodes |> reportFailedNodes
