module Notification
open System


[<RequireQualifiedAccess>]
type PrinterProtocol =
    | BuildStarted of graph:Graph.WorkspaceGraph
    | BuildCompleted of summary:Build.BuildSummary
    | BuildNodeScheduled of node:Graph.Node
    | BuildNodeStarted of node:Graph.Node
    | BuildNodeCompleted of node:Graph.Node * summary:Cache.TargetSummary option
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
        let rec messageLoop s= async {
            let! msg = inbox.Receive()
            match msg with
            | PrinterProtocol.BuildStarted graph -> 
                Console.WriteLine($"{Ansi.Emojis.rocket} Running target {graph.Target}")
                scheduleUpdate ()
                return! messageLoop () 

            | PrinterProtocol.BuildCompleted summary ->
                for failedSummary in failedLogs do
                    let lastLog = failedSummary.Steps |> List.last
                    Console.WriteLine($"{Ansi.Styles.red}{failedSummary.Target} {failedSummary.Project}: {lastLog.Command} {lastLog.Arguments}{Ansi.Styles.reset}")
                    let log = IO.readTextFile lastLog.Log
                    Console.WriteLine(log)

                let result =
                    match summary.Status with
                    | Build.BuildStatus.Success -> Ansi.Emojis.happy
                    | _ -> Ansi.Emojis.sad

                let msg = $"{result} Completed in {summary.Duration}"
                Console.Out.WriteLine(msg)

                // let jsonBuildInfo = Json.Serialize summary
                // Console.Out.WriteLine($"{jsonBuildInfo}")
                buildComplete.Set() |> ignore

            | PrinterProtocol.BuildNodeScheduled node ->
                let label = $"{node.TargetId} {node.ProjectId}"
                let status = Progress.Scheduled DateTime.Now
                renderer.Update label status
                scheduleUpdate ()
                return! messageLoop ()

            | PrinterProtocol.BuildNodeStarted node ->
                let label = $"{node.TargetId} {node.ProjectId}"
                let status = Progress.Progress DateTime.Now
                renderer.Update label status
                scheduleUpdate ()
                return! messageLoop ()

            | PrinterProtocol.BuildNodeCompleted (node, summary) ->
                let status =
                    match summary with
                    | Some summary ->
                        match summary.Status with
                        | Cache.TaskStatus.Success -> Progress.Success
                        | _ ->
                            failedLogs <- failedLogs @ [ summary ]
                            Progress.Fail
                    | _ -> Progress.Fail

                let label = $"{node.TargetId} {node.ProjectId}"
                renderer.Update label status

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
            graph |> PrinterProtocol.BuildStarted |> printerAgent.Post

        member _.BuildCompleted(summary: Build.BuildSummary) = 
            summary
            |> PrinterProtocol.BuildCompleted
            |> printerAgent.Post

        member _.BuildNodeScheduled(node: Graph.Node) = 
            node
            |> PrinterProtocol.BuildNodeScheduled
            |> printerAgent.Post

        member _.BuildNodeStarted(node: Graph.Node) = 
            node
            |> PrinterProtocol.BuildNodeStarted
            |> printerAgent.Post

        member _.BuildNodeCompleted (node: Graph.Node) (status: Cache.TargetSummary option) = 
            (node, status)
            |> PrinterProtocol.BuildNodeCompleted
            |> printerAgent.Post
