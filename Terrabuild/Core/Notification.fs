module Notification
open System

[<RequireQualifiedAccess>]
type BuildStatus =
    | Success
    | Failure

[<RequireQualifiedAccess>]
type TaskBuildStatus =
    | Success of string
    | Failure of string
    | Unfulfilled of string

[<RequireQualifiedAccess>]
type BuildSummary = {
    Commit: string
    StartedAt: DateTime
    EndedAt: DateTime
    Duration: TimeSpan
    Status: BuildStatus
    Target: string
    Dependencies: Map<string, TaskBuildStatus>
}

type IBuildNotification =
    abstract WaitCompletion: unit -> unit
    abstract BuildStarted: unit -> unit
    abstract BuildCompleted: buildSummary:BuildSummary -> unit
    abstract BuildNodeStarted: node:Graph.Node -> unit
    abstract BuildNodeCompleted: node:Graph.Node -> status:TaskBuildStatus -> unit

[<RequireQualifiedAccess>]
type PrinterProtocol =
    | BuildStarted
    | BuildCompleted of summaryFilename:BuildSummary
    | BuildNodeStarted of node:Graph.Node
    | BuildNodeCompleted of node:Graph.Node * status:TaskBuildStatus
    | Render

type BuildNotification() =

    let buildComplete = new System.Threading.ManualResetEvent(false)
    let renderer = Progress.ProgressRenderer()
    let updateTimer = 100

    let handler (inbox: MailboxProcessor<PrinterProtocol>) =

        let scheduleUpdate () =
            System.Threading.Tasks.Task.Delay(updateTimer)
                  .ContinueWith(fun _ -> PrinterProtocol.Render |> inbox.Post) |> ignore

        // the message processing function
        let rec messageLoop s= async {
            let! msg = inbox.Receive()
            match msg with
            | PrinterProtocol.BuildStarted -> 
                scheduleUpdate ()
                return! messageLoop () 

            | PrinterProtocol.BuildCompleted summary ->
                let msg = $"Completed in {summary.Duration}"
                Console.Out.WriteLine(msg)

                // let jsonBuildInfo = Json.Serialize summary
                // Console.Out.WriteLine($"{jsonBuildInfo}")
                buildComplete.Set() |> ignore

            | PrinterProtocol.BuildNodeStarted node ->
                let label = $"{node.TargetId} {node.ProjectId}"
                let status = Progress.Progress DateTime.Now
                renderer.Add label status
                scheduleUpdate ()
                return! messageLoop ()

            | PrinterProtocol.BuildNodeCompleted (node, status) ->
                let status =
                    match status with
                    | TaskBuildStatus.Success _ -> Progress.Success
                    | TaskBuildStatus.Failure _ -> Progress.Fail
                    | TaskBuildStatus.Unfulfilled _ -> Progress.Fail

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

    interface IBuildNotification with
        member _.WaitCompletion(): unit = 
            buildComplete.WaitOne() |> ignore

        member _.BuildStarted() =
            PrinterProtocol.BuildStarted |> printerAgent.Post

        member _.BuildCompleted(summary: BuildSummary) = 
            summary
            |> PrinterProtocol.BuildCompleted
            |> printerAgent.Post

        member _.BuildNodeStarted(node: Graph.Node) = 
            node
            |> PrinterProtocol.BuildNodeStarted
            |> printerAgent.Post

        member _.BuildNodeCompleted (node: Graph.Node) (status: TaskBuildStatus) = 
            (node, status)
            |> PrinterProtocol.BuildNodeCompleted
            |> printerAgent.Post
