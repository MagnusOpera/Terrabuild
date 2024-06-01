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
    | BuildStarted of graph:Graph.Workspace
    | BuildCompleted of summary:Build.Summary
    | NodeStatusChanged of node:Graph.Node * status:NodeStatus
    | NodeCompleted of node:Graph.Node * restored:bool * summary:Cache.TargetSummary option
    | Render

type BuildNotification() =

    let buildComplete = new System.Threading.ManualResetEvent(false)
    let renderer = Progress.ProgressRenderer()
    let updateTimer = 200

    let handler (inbox: MailboxProcessor<PrinterProtocol>) =

        let scheduleUpdate () =
            System.Threading.Tasks.Task.Delay(updateTimer)
                  .ContinueWith(fun _ -> PrinterProtocol.Render |> inbox.Post) |> ignore

        // the message processing function
        let rec messageLoop () = async {
            let! msg = inbox.Receive()
            match msg with
            | PrinterProtocol.BuildStarted graph -> 
                scheduleUpdate ()
                return! messageLoop () 

            | PrinterProtocol.BuildCompleted summary ->
                renderer.Refresh()
                buildComplete.Set() |> ignore

            | PrinterProtocol.NodeStatusChanged (node, status) ->
                let spinner, frequency =
                    match status with
                    | NodeStatus.Scheduled -> spinnerScheduled, frequencyScheduled
                    | NodeStatus.Downloading -> spinnerDownload, frequencyDownload
                    | NodeStatus.Uploading -> spinnerUpload, frequencyUpload
                    | NodeStatus.Building -> spinnerBuilding, frequencyBuilding
                renderer.Update node.Hash node.Label spinner frequency
                scheduleUpdate ()
                return! messageLoop ()

            | PrinterProtocol.NodeCompleted (node, restored, summary) ->
                let status =
                    match summary with
                    | Some summary -> summary.Status = Cache.TaskStatus.Success
                    | _ -> false

                let label = $"{node.Label} {node.Project}"
                renderer.Complete node.Hash label status restored
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

        member _.BuildCompleted(summary: Build.Summary) = 
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

        member _.NodeCompleted (node: Graph.Node) (restored: bool) (status: Cache.TargetSummary option) = 
            PrinterProtocol.NodeCompleted (node, restored, status)
            |> printerAgent.Post
