module Notification
open System

// https://antofthy.gitlab.io/info/ascii/HeartBeats_howto.txt
let spinnerScheduled = "⠁⠂⠄⠂"
let frequencyScheduled = 200.0

let spinnerUpload = "↑  ↑ ↑ "
let frequencyUpload = 200.0

let spinnerDownload = "↓  ↓ ↓ "
let frequencyDownload = 200.0

let spinnerBuilding = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏"
let frequencyBuilding = 100.0


[<RequireQualifiedAccess>]
type NodeStatus =
    | Scheduled
    | Building
    | Downloading
    | Uploading

[<RequireQualifiedAccess>]
type PrinterProtocol =
    | BuildStarted of graph:Graph.WorkspaceGraph
    | BuildCompleted of summary:Build.BuildSummary
    | NodeStatusChanged of node:Graph.Node * status:NodeStatus
    | NodeCompleted of node:Graph.Node * summary:Cache.TargetSummary option
    | Render

type BuildNotification() =

    let buildComplete = new System.Threading.ManualResetEvent(false)
    let renderer = Progress.ProgressRenderer()
    let updateTimer = 200

    let mutable failedLogs : Cache.TargetSummary list = []

    let handler (inbox: MailboxProcessor<PrinterProtocol>) =

        let scheduleUpdate () =
            System.Threading.Tasks.Task.Delay(updateTimer)
                  .ContinueWith(fun _ -> PrinterProtocol.Render |> inbox.Post) |> ignore

        // the message processing function
        let rec messageLoop () = async {
            let! msg = inbox.Receive()
            match msg with
            | PrinterProtocol.BuildStarted graph -> 
                let targets = graph.Targets |> String.join ","
                let targetLabel = if graph.Targets.Count > 1 then "targets" else "target"
                $"{Ansi.Emojis.rocket} Running {targetLabel} {targets}" |> Terminal.writeLine
                scheduleUpdate ()
                return! messageLoop () 

            | PrinterProtocol.BuildCompleted summary ->
                renderer.Refresh()
                for failedSummary in failedLogs do
                    let lastLog = failedSummary.Steps |> List.last

                    let containered =
                        match lastLog.Container with
                        | None -> ""
                        | Some container -> $"{{{container}}} "
                    $"{Ansi.Emojis.prohibited} {Ansi.Styles.red}{failedSummary.Target} {failedSummary.Project}: {containered}{lastLog.Command} {lastLog.Arguments}{Ansi.Styles.reset}"
                    |> Terminal.writeLine

                    let log = IO.readTextFile lastLog.Log
                    log |> Terminal.writeLine

                let result =
                    match summary.Status with
                    | Build.BuildStatus.Success -> Ansi.Emojis.happy
                    | _ -> Ansi.Emojis.sad

                $"{result} Completed in {summary.Duration}"
                |> Terminal.writeLine

                buildComplete.Set() |> ignore

            | PrinterProtocol.NodeStatusChanged (node, status) ->
                let spinner, frequency =
                    match status with
                    | NodeStatus.Scheduled -> spinnerScheduled, frequencyScheduled
                    | NodeStatus.Downloading -> spinnerDownload, frequencyDownload
                    | NodeStatus.Uploading -> spinnerUpload, frequencyUpload
                    | NodeStatus.Building -> spinnerBuilding, frequencyBuilding
                let label = $"{node.Target} {node.Project}"
                renderer.Update node.Hash label spinner frequency
                scheduleUpdate ()
                return! messageLoop ()

            | PrinterProtocol.NodeCompleted (node, summary) ->
                let status =
                    match summary with
                    | Some summary ->
                        match summary.Status with
                        | Cache.TaskStatus.Success -> true
                        | _ ->
                            failedLogs <- failedLogs @ [ summary ]
                            false
                    | _ -> false

                let label = $"{node.Target} {node.Project}"
                renderer.Complete node.Hash label status
                scheduleUpdate ()
                return! messageLoop ()

            | PrinterProtocol.Render ->
                renderer.Refresh ()
                scheduleUpdate()
                return! messageLoop ()
        }

        // start the loop
        messageLoop()

    let printerAgent = MailboxProcessor.Start(handler)

    interface Build.IBuildNotification with
        member _.WaitCompletion(): unit = 
            buildComplete.WaitOne() |> ignore

        member _.BuildStarted graph =
            PrinterProtocol.BuildStarted graph
            |> printerAgent.Post

        member _.BuildCompleted(summary: Build.BuildSummary) = 
            PrinterProtocol.BuildCompleted summary
            |> printerAgent.Post

        member _.NodeScheduled(node: Graph.Node) =
            PrinterProtocol.NodeStatusChanged (node, NodeStatus.Scheduled)
            |> printerAgent.Post

        member _.NodeDownloading(node: Graph.Node) = 
            PrinterProtocol.NodeStatusChanged (node, NodeStatus.Downloading)
            |> printerAgent.Post

        member _.NodeBuilding(node: Graph.Node) = 
            PrinterProtocol.NodeStatusChanged (node, NodeStatus.Building)
            |> printerAgent.Post

        member _.NodeUploading(node: Graph.Node) = 
            PrinterProtocol.NodeStatusChanged (node, NodeStatus.Uploading)
            |> printerAgent.Post

        member _.NodeCompleted (node: Graph.Node) (status: Cache.TargetSummary option) = 
            PrinterProtocol.NodeCompleted (node, status)
            |> printerAgent.Post
