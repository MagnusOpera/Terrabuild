module Logs
open Cache
open System

module Iconography =
    let restore_ok = Ansi.Emojis.popcorn
    let restore_ko = Ansi.Emojis.pretzel
    let build_ok = Ansi.Emojis.green_checkmark
    let build_ko = Ansi.Emojis.red_cross
    let task_pending = Ansi.Emojis.construction
    let task_unknown = Ansi.Emojis.bang_mark


let dumpLogs (logId: Guid) (options: ConfigOptions.Options) (cache: ICache) (graph: GraphDef.Graph) (summary: Build.Summary) =
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
            | _ -> Iconography.task_unknown

        let dumpMarkdown (node: GraphDef.Node) =
            let header =
                let statusEmoji = statusEmoji node
                let uniqueId = stableRandomId node.Id
                $"## <a name=\"user-content-{uniqueId}\"></a> {statusEmoji} {node.Target} {node.ProjectDir}"

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
            $"| {statusEmoji} [{node.Target} {node.ProjectDir}](#user-content-{uniqueId}) | {duration} |" |> append
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
        $"| Total Cost | {cost} |" |> append
        $"| Total Gain | {gain} |" |> append
        if options.WhatIf |> not then
            $"| Duration | {summary.EndedAt - options.StartedAt} |" |> append

        "" |> append

        let getNodeStatus (node: GraphDef.Node) =
            match originSummaries |> Map.tryFind node.Id with
            | Some _ -> statusEmoji node
            | _ -> Iconography.task_pending

        let getOrigin (node: GraphDef.Node) =
            summary.Nodes |> Map.tryFind node.Id |> Option.map (fun nodeInfo -> nodeInfo.Request)

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
            let label = $"{node.Target} {node.ProjectDir}"

            let getHeaderFooter success =
                let color =
                    if success then $"{Ansi.Styles.green}{Ansi.Emojis.checkmark}"
                    else $"{Ansi.Styles.red}{Ansi.Emojis.crossmark}"

                $"{color} {label}{Ansi.Styles.reset}", ""

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

                    getHeaderFooter summary.IsSuccessful, dumpLogs
                | _ ->
                    let dumpNoLog() = $"{Ansi.Styles.yellow}No logs available{Ansi.Styles.reset}" |> Terminal.writeLine
                    getHeaderFooter false, dumpNoLog

            logStart |> Terminal.writeLine
            dumpLogs ()
            logEnd |> Terminal.writeLine

        nodes
        |> Seq.filter (fun node -> summary.Nodes |> Map.containsKey node.Id)
        |> Seq.iter dumpTerminal


    let dumpGitHubActions (nodes: GraphDef.Node seq) =
        let dumpTerminal (node: GraphDef.Node) =
            let label = $"{node.Target} {node.ProjectDir}"
            let cacheEntryId = GraphDef.buildCacheKey node
            let summary = cache.TryGetSummaryOnly false cacheEntryId

            match summary with
            | Some (_, summary) ->
                if summary.IsSuccessful |> not then
                    $"::error title=build failed::{label}" |> Terminal.writeLine
                    match summary.Operations |> List.tryLast with
                    | Some command ->
                        match command |> List.tryLast with
                        | Some operation ->
                            $"::group::{operation.MetaCommand}" |> Terminal.writeLine
                            operation.Log |> IO.readTextFile |> Terminal.write
                            $"::endgroup::" |> Terminal.writeLine
                        | _ -> ()
                    | _ -> ()
            | None ->
                $"::warning title=no logs::{label}" |> Terminal.writeLine

        nodes
        |> Seq.filter (fun node -> summary.Nodes |> Map.containsKey node.Id)
        |> Seq.iter dumpTerminal



    let logger =
        let dump nodes logType =
            match logType with
            | Contracts.Markdown filename -> dumpMarkdown filename nodes
            | Contracts.Terminal -> dumpTerminal nodes
            | Contracts.GitHubActions -> dumpGitHubActions nodes
        fun nodes -> options.LogTypes |> List.iter (dump nodes)

    let sortedNodes =
        summary.Nodes |> Map.map (fun nodeId _ -> graph.Nodes[nodeId])
        |> Seq.map (fun (KeyValue(_, node)) -> node)
        |> Seq.sortBy (fun node ->
            match summary.Nodes |> Map.tryFind node.Id with
            | Some nodeInfo ->
                match nodeInfo.Status with
                | Build.TaskStatus.Success completionDate -> completionDate
                | Build.TaskStatus.Failure (completionDate, _) -> completionDate
                | _ -> DateTime.MaxValue
            | _ -> DateTime.MaxValue)
        |> List.ofSeq

    sortedNodes |> logger
