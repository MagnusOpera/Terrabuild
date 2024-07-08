module Logs
open Cache
open System




let dumpLogs (logId: Guid) (options: Configuration.Options) (cache: ICache) (sourceControl: Contracts.ISourceControl) (graph: GraphDef.Graph) =
    let stableRandomId (id: string) =
        $"{logId} {id}" |> Hash.md5 |> String.toLower

    // filter, collect summaries and dump
    let nodes =
        graph.Nodes
        |> Map.values
        |> Seq.map (fun node ->
            let cacheEntryId = GraphDef.buildCacheKey node
            let originSummary = cache.TryGetSummaryOnly false cacheEntryId
            node, originSummary)
        |> Seq.sortBy (fun (_, originSummary) -> originSummary |> Option.map (fun (_, summary) -> summary.EndedAt))
        |> List.ofSeq

    let successful =
        nodes
        |> List.forall (fun (_, originSummary) ->
            match originSummary with
            | Some (_, summary) -> summary.IsSuccessful
            | _ -> false)

    let dumpMarkdown filename (infos: (GraphDef.Node * (Origin*TargetSummary) option) list) =
        let appendLines lines = IO.appendLinesFile filename lines 
        let append line = appendLines [line]

        let statusEmoji (originSummary: (Origin * TargetSummary) option) =
            match originSummary with
            | Some (origin, summary) ->
                match summary.IsSuccessful, origin with
                | true, Origin.Remote -> "🍿"
                | true, Origin.Local -> "✅"
                | false, Origin.Remote -> "🥨"
                | false, Origin.Local -> "❌"
            | _ -> "❓"


        let dumpMarkdown (node: GraphDef.Node) (originSummary: (Origin*TargetSummary) option) =
            let header =
                let statusEmoji = statusEmoji originSummary
                let uniqueId = stableRandomId node.Id
                $"## <a name=\"user-content-{uniqueId}\"></a> {statusEmoji} {node.Label}"

            let dumpLogs =
                match originSummary with
                | Some (_, summary) -> 
                    let dumpLogs () =
                        let batchNode =
                            match node.IsBatched, node.Dependencies |> Seq.tryHead with
                            | true, Some batchId -> Some graph.Nodes[batchId]
                            | _ -> None

                        match batchNode with
                        | Some batchNode ->
                            let uniqueId = stableRandomId batchNode.Id
                            $"**Batched with [{batchNode.Label}](#user-content-{uniqueId})**" |> append
                        | _ ->
                            summary.Operations |> List.iter (fun group ->
                                group |> List.iter (fun step ->
                                    $"### {step.MetaCommand}" |> append
                                    if options.Debug then
                                        let cmd = $"{step.Command} {step.Arguments}" |> String.trim
                                        $"*{cmd}*" |> append

                                    append "```"
                                    step.Log |> IO.readTextFile |> append
                                    append "```"
                                )
                            )
                    dumpLogs
                | _ ->
                    let dumpNoLog() = $"**No logs available**" |> append
                    dumpNoLog

            header |> append
            dumpLogs ()

        let targets = options.Targets |> String.join " "
        let message, color =
            if successful then "success", "success"
            else "failure", "critical"
        let targetsBadge = options.Targets |> String.join "_"
        let summaryAnchor = stableRandomId "summary"
        $"[![{targets}](https://img.shields.io/badge/{targetsBadge}-build_{message}-{color})](#user-content-{summaryAnchor})" |> append

        $"<details><summary>Expand for details</summary>" |> append

        "" |> append
        $"# <a name=\"user-content-{summaryAnchor}\"></a> Summary" |> append
        "| Target | Duration |" |> append
        "|--------|----------|" |> append
        infos |> List.iter (fun (node, originSummary) ->
            if node.IsLast then
                let statusEmoji = statusEmoji originSummary
                let duration =
                    match originSummary with
                    | Some (_, summary) -> $"{summary.Duration}"
                    | _ -> ""

                let uniqueId = stableRandomId node.Id
                $"| {statusEmoji} [{node.Label}](#user-content-{uniqueId}) | {duration} |" |> append
        )
        let (cost, gain) =
            infos |> List.fold (fun (cost, gain) (node, originSummary) ->
                match originSummary with
                | Some (origin, summary) when node.IsLast ->
                    let duration = summary.Duration
                    if origin = Origin.Local then cost + duration, gain
                    else cost, gain + duration
                | _ -> cost, gain
            ) (TimeSpan.Zero, TimeSpan.Zero)
        $"| Cost | {cost} |" |> append
        $"| Gain | {gain} |" |> append

        "" |> append

        let mapNodes =
            nodes
            |> List.map (fun (node, originSummary) -> node.Id, originSummary)
            |> Map.ofList

        let getNodeStatus id =
            match mapNodes |> Map.tryFind id with
            | Some originSummary -> statusEmoji originSummary
            | _ -> "🫥"

        let mermaid = GraphDef.render (Some getNodeStatus) graph
        $"# Build Graph" |> append
        "```mermaid" |> append
        mermaid |> appendLines
        "```" |> append

        "" |> append
        "# Details" |> append
        infos |> List.iter (fun (node, summary) -> dumpMarkdown node summary)
        "" |> append

        "</details>" |> append

    let dumpTerminal (infos: (GraphDef.Node * (Origin*TargetSummary) option) seq) =
        let dumpTerminal (node: GraphDef.Node) (originSummary: (Origin*TargetSummary) option) =
            let title = node.Label

            let getHeaderFooter success title =
                let color =
                    if success then $"{Ansi.Styles.green}{Ansi.Emojis.checkmark}"
                    else $"{Ansi.Styles.red}{Ansi.Emojis.crossmark}"

                $"{color} {title}{Ansi.Styles.reset}", ""

            let (logStart, logEnd), dumpLogs =
                match originSummary with
                | Some (_, summary) -> 
                    let dumpLogs () =

                        let batchNode =
                            match node.IsBatched, node.Dependencies |> Seq.tryHead with
                            | true, Some batchId -> Some graph.Nodes[batchId]
                            | _ -> None

                        match batchNode with
                        | Some batchNode -> $"{Ansi.Styles.yellow}Batched with '{batchNode.Label}'{Ansi.Styles.reset}" |> Terminal.writeLine
                        | _ ->
                            summary.Operations |> Seq.iter (fun group ->
                                group |> Seq.iter (fun step ->
                                    $"{Ansi.Styles.yellow}{step.MetaCommand}{Ansi.Styles.reset}" |> Terminal.writeLine
                                    if options.Debug then
                                        $"{Ansi.Styles.cyan}{step.Command} {step.Arguments}{Ansi.Styles.reset}" |> Terminal.writeLine
                                    step.Log |> IO.readTextFile |> Terminal.write
                                )
                            )

                    getHeaderFooter summary.IsSuccessful title, dumpLogs
                | _ ->
                    let dumpNoLog() = $"{Ansi.Styles.yellow}No logs available{Ansi.Styles.reset}" |> Terminal.writeLine
                    getHeaderFooter false title, dumpNoLog

            logStart |> Terminal.writeLine
            dumpLogs ()
            logEnd |> Terminal.writeLine

        infos |> Seq.iter (fun (node, summary) -> dumpTerminal node summary)

    let reportFailedNodes (infos: (GraphDef.Node * (Origin * TargetSummary) option) list) =
        infos
        |> List.iter (fun (node, originSummary) ->
            match originSummary with
            | Some (_, summary) when summary.IsSuccessful |> not -> sourceControl.LogError $"{node.Label} failed"
            | _ -> ())

    let logger =
        match sourceControl.LogType with
        | Contracts.Terminal -> dumpTerminal
        | Contracts.Markdown filename -> dumpMarkdown filename

    nodes |> logger
    nodes |> reportFailedNodes
