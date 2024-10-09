module Logs
open Cache
open System

module Iconography =
    let restore_ok = Ansi.Emojis.popcorn
    let restore_ko = Ansi.Emojis.pretzel
    let build_ok = Ansi.Emojis.green_checkmark
    let build_ko = Ansi.Emojis.red_cross
    let task_pending = Ansi.Emojis.coffee


let dumpLogs (logId: Guid) (options: Configuration.Options) (cache: ICache) (sourceControl: Contracts.ISourceControl) (graph: GraphDef.Graph) (summary: Build.Summary) =
    let stableRandomId (id: string) =
        $"{logId} {id}" |> Hash.md5 |> String.toLower


    let dumpMarkdown filename (nodes: GraphDef.Node seq) =
        let originSummaries =
            nodes
            |> Seq.map (fun node ->
                let cacheEntryId = GraphDef.buildCacheKey node
                node.Id, cache.TryGetSummaryOnly false cacheEntryId)
            |> Map.ofSeq

        let successful = summary.IsSuccess
        let appendLines lines = IO.appendLinesFile filename lines 
        let append line = appendLines [line]

        let statusEmoji (node: GraphDef.Node) =
            match summary.Nodes |> Map.tryFind node.Id with
            | Some nodeInfo ->
                match nodeInfo.Request, nodeInfo.Status with
                | Build.TaskRequest.Restore, Build.TaskStatus.Success _ -> Iconography.restore_ok
                | Build.TaskRequest.Restore, Build.TaskStatus.Failure _ -> Iconography.restore_ko
                | Build.TaskRequest.Build, Build.TaskStatus.Success _ -> Iconography.build_ok
                | Build.TaskRequest.Build, Build.TaskStatus.Failure _ -> Iconography.build_ko
            | _ -> Iconography.task_pending

        let dumpMarkdown (node: GraphDef.Node) =
            let header =
                let statusEmoji = statusEmoji node
                let uniqueId = stableRandomId node.Id
                $"## <a name=\"user-content-{uniqueId}\"></a> {statusEmoji} {node.Label}"

            let dumpLogs =
                let originSummary = originSummaries[node.Id]
                match originSummary with
                | Some (_, summary) -> 
                    let dumpLogs () =
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

        nodes |> Seq.iter (fun node ->
            let originSummary = originSummaries[node.Id]
            let statusEmoji = statusEmoji node
            let duration =
                match originSummary with
                | Some (_, summary) -> $"{summary.Duration}"
                | _ -> ""

            let uniqueId = stableRandomId node.Id
            $"| {statusEmoji} [{node.Label}](#user-content-{uniqueId}) | {duration} |" |> append
        )
        let (cost, gain) =
            originSummaries |> Map.fold (fun (cost, gain) _ originSummary ->
                match originSummary with
                | Some (origin, summary) ->
                    let duration = summary.Duration
                    if origin = Origin.Local then cost + duration, gain
                    else cost, gain + duration
                | _ -> cost, gain
            ) (TimeSpan.Zero, TimeSpan.Zero)
        $"| Cost | {cost} |" |> append
        $"| Gain | {gain} |" |> append

        "" |> append

        let getNodeStatus (node: GraphDef.Node) =
            match originSummaries |> Map.tryFind node.Id with
            | Some _ -> statusEmoji node
            | _ -> Iconography.task_pending

        let getOrigin (node: GraphDef.Node) =
            match originSummaries |> Map.tryFind node.Id with
            | Some (Some (origin, _)) -> Some origin
            | _ -> None

        // TODO: pass build action getter
        let mermaid = Mermaid.render (Some getNodeStatus) (Some getOrigin) graph
        $"# Build Graph" |> append
        "```mermaid" |> append
        mermaid |> appendLines
        "```" |> append

        "" |> append
        "# Details" |> append
        nodes
        |> Seq.filter (fun node -> summary.Nodes |> Map.containsKey node.Id)
        |> Seq.iter dumpMarkdown
        "" |> append

        "</details>" |> append
        "" |> append



    let dumpTerminal (nodes: GraphDef.Node seq) =
        let dumpTerminal (node: GraphDef.Node) =
            let title = node.Label

            let getHeaderFooter success title =
                let color =
                    if success then $"{Ansi.Styles.green}{Ansi.Emojis.checkmark}"
                    else $"{Ansi.Styles.red}{Ansi.Emojis.crossmark}"

                $"{color} {title}{Ansi.Styles.reset}", ""

            let (logStart, logEnd), dumpLogs =
                let cacheEntryId = GraphDef.buildCacheKey node
                let originSummary = cache.TryGetSummaryOnly false cacheEntryId
                match originSummary with
                | Some (_, summary) -> 
                    let dumpLogs () =
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

        nodes
        |> Seq.filter (fun node -> summary.Nodes |> Map.containsKey node.Id)
        |> Seq.iter dumpTerminal

    let logger =
        match sourceControl.LogType with
        | Contracts.Markdown filename -> dumpMarkdown filename
        | _ -> dumpTerminal

    let sortedNodes =
        graph.Nodes
        |> Seq.map (fun (KeyValue(_, node)) -> node)
        |> Seq.sortBy (fun node ->
            match summary.Nodes |> Map.tryFind node.Id with
            | Some nodeInfo ->
                match nodeInfo.Status with
                | Build.TaskStatus.Success completionDate -> completionDate
                | Build.TaskStatus.Failure (completionDate, _) -> completionDate
            | _ -> DateTime.MaxValue)
        |> List.ofSeq

    sortedNodes |> logger
