module Progress
open System
open System.Text


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


type ProgressItem = {
    Project: string
    Target: string
    Offset: int
    mutable Status: TaskBuildStatus option
}


type BuildNotification() =

    let buildComplete = new System.Threading.ManualResetEvent(false)

    let handler (inbox: MailboxProcessor<PrinterProtocol>) =
        let spinner = [ "⠋"; "⠙"; "⠹"; "⠸"; "⠼"; "⠴";  "⠦"; "⠧"; "⠇"; "⠏" ]
        let updateDelay = 100
        let resolution = 100
        let ESC = "\u001b"
        let CSI = ESC + "["
        let EL2 = $"{CSI}2K"
        let CUU1 = $"{CSI}1A"
        let crossmark = "\U0000274C"
        let checkmark = "\U00002714"
        let green = $"{CSI}32m"
        let red = $"{CSI}91m"
        let yellow = $"{CSI}33m"
        let normal = $"{CSI}0m"

        let clear count =
            let eraseLine = "\r" + EL2 + CUU1 + EL2
            let eraseLines =
                [1..count]
                |> List.fold (fun (acc:StringBuilder) _ -> acc.Append(eraseLine)) (StringBuilder())
            Console.Out.Write(eraseLines)
            Console.Out.Flush()

        let render (turn: int) (items: ProgressItem list) =
            for item in items do
                let status =
                    match item.Status with
                    | Some (TaskBuildStatus.Success _) -> green + checkmark + normal
                    | Some (TaskBuildStatus.Failure _) -> red + crossmark + normal
                    | Some (TaskBuildStatus.Unfulfilled _) -> red + crossmark + normal
                    | _ -> yellow + spinner[(turn + item.Offset) % spinner.Length] + normal
                Console.Out.WriteLine($"{status} {item.Target} {item.Project}")
            Console.Out.Flush()

        let scheduleUpdate () =
            System.Threading.Tasks.Task.Delay(updateDelay).ContinueWith(fun t -> PrinterProtocol.Render |> inbox.Post) |> ignore

        // the message processing function
        let rec messageLoop (items: ProgressItem list) = async {
            let turn = DateTime.Now.Millisecond / resolution
            let! msg = inbox.Receive()
            match msg with
            | PrinterProtocol.BuildStarted -> 
                scheduleUpdate()
                return! messageLoop items 

            | PrinterProtocol.BuildCompleted summary ->
                let jsonBuildInfo = Json.Serialize summary
                Console.Out.WriteLine($"{jsonBuildInfo}")
                buildComplete.Set() |> ignore

            | PrinterProtocol.BuildNodeStarted node ->
                let newItem = { Project = node.ProjectId
                                Target = node.TargetId
                                Status = None
                                Offset = (DateTime.Now.Millisecond |> int) % spinner.Length }
                let newItems = items @ [ newItem ]
                clear items.Length
                render turn newItems
                scheduleUpdate()
                return! messageLoop newItems

            | PrinterProtocol.BuildNodeCompleted (node, status) ->
                let item = items |> List.find (fun x -> x.Project = node.ProjectId && x.Target = node.TargetId)
                item.Status <- Some status
                clear items.Length
                render turn items
                scheduleUpdate()
                return! messageLoop items  

            | PrinterProtocol.Render ->
                clear items.Length
                render turn items
                scheduleUpdate()
                return! messageLoop items  
        }

        // start the loop
        messageLoop []

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
