module Notification
open System.Threading
open System.Collections.Concurrent
open Spectre.Console
open Spectre.Console.Rendering
open System.Collections.Generic
open System

[<RequireQualifiedAccess>]
type NodeStatus =
    | Scheduled
    | Building
    | Downloading
    | Uploading

[<RequireQualifiedAccess>]
type PrinterProtocol =
    | BuildStarted of graph:GraphDef.Graph
    | BuildCompleted of summary:Build.Summary
    | NodeStatusChanged of node:GraphDef.Node * status:NodeStatus
    | NodeOutput of node:GraphDef.Node * line:string
    | NodeCompleted of node:GraphDef.Node * status:Build.TaskRequest * success:bool
    | Render


type private NodeInfo() =
    let nodeTitle = Markup("")
    let tableColumn = TableColumn(nodeTitle)
    let grid = Grid().AddColumn()
    let table = Table().NoBorder().AddColumn(tableColumn).AddRow(grid)

    let renderable: IRenderable = table
    interface IRenderable with        
        member _.Measure(options, maxWidth) = renderable.Measure(options, maxWidth)
        member _.Render(options, maxWidth) = renderable.Render(options, maxWidth)

    member _.SetTitle(title: string) =
        nodeTitle.Update(title) |> ignore

    member _.AddRow(row: string) =
        if row |> isNull |> not then
            grid.AddRow(Text(row)) |> ignore
            let rows = grid.Rows :?> IList<GridRow>
            if rows.Count > 5 then rows.RemoveAt(0) |> ignore


type BuildNotification() =
    let queueTrigger = new AutoResetEvent(false)
    let queue = ConcurrentQueue<PrinterProtocol>()
    let buildComplete = new ManualResetEvent(false)

    let handler () =
        let mutable nodeInfos = Map.empty
        let table = Table().HideHeaders().NoBorder().AddColumn("Output")

        let rec processMessages (ctx: LiveDisplayContext) =
            let getOrCreateNode node =
                match nodeInfos |> Map.tryFind node with
                | Some nodeInfo -> nodeInfo
                | _ ->
                    let nodeInfo = NodeInfo()
                    nodeInfos <- nodeInfos |> Map.add node nodeInfo
                    table.AddRow(nodeInfo) |> ignore
                    nodeInfo

            let updateNode node (content: string) =
                let nodeInfo = getOrCreateNode node
                nodeInfo.AddRow(content)

            let updateNodeTitle node title =
                let nodeInfo = getOrCreateNode node
                nodeInfo.SetTitle(title)

            let mutable continueProcessing = true
            queueTrigger.WaitOne() |> ignore

            let rec dequeueAll() =
                match queue.TryDequeue() with
                | true, msg ->
                    match msg with
                    | PrinterProtocol.BuildStarted _ -> ()

                    | PrinterProtocol.BuildCompleted _ ->
                        continueProcessing <- false

                    | PrinterProtocol.NodeStatusChanged (node, status) ->
                        let icon =
                            match status with
                            | NodeStatus.Scheduled -> Ansi.Emojis.clock
                            | NodeStatus.Downloading -> Ansi.Emojis.down
                            | NodeStatus.Uploading -> Ansi.Emojis.up
                            | NodeStatus.Building -> Ansi.Emojis.clockwise
                        updateNodeTitle node.Id $"{icon} [bold yellow]{node.Label} {node.Project}[/]"

                    | PrinterProtocol.NodeOutput (node, line) ->
                        updateNode node.Id line

                    | PrinterProtocol.NodeCompleted (node, status, success) ->
                        let icon, color =
                            match status, success with
                            | Build.TaskRequest.Restore, true -> Ansi.Emojis.popcorn, Color.Green
                            | Build.TaskRequest.Restore, false -> Ansi.Emojis.pretzel, Color.Red
                            | Build.TaskRequest.Build, true -> Ansi.Emojis.green_checkmark, Color.Green
                            | Build.TaskRequest.Build, false -> Ansi.Emojis.red_cross, Color.Red
                        updateNodeTitle node.Id $"{icon} [bold {color}]{node.Label} {node.Project}[/]"

                    | PrinterProtocol.Render -> ctx.Refresh()
                    if continueProcessing then dequeueAll()
                | _ -> ()

            dequeueAll()
            ctx.Refresh()
            if continueProcessing then processMessages ctx

        AnsiConsole.Live(table).Start(processMessages)
        buildComplete.Set() |> ignore


    do
        Thread(handler).Start()

    let post msg =
        queue.Enqueue msg
        queueTrigger.Set() |> ignore

    interface Build.IBuildNotification with
        member _.WaitCompletion() =
            buildComplete.WaitOne() |> ignore

        member _.BuildStarted graph =
            PrinterProtocol.BuildStarted graph |> post

        member _.BuildCompleted summary =
            PrinterProtocol.BuildCompleted summary |> post

        member _.NodeScheduled node =
            PrinterProtocol.NodeStatusChanged (node, NodeStatus.Scheduled) |> post

        member _.NodeDownloading node =
            PrinterProtocol.NodeStatusChanged (node, NodeStatus.Downloading) |> post

        member _.NodeBuilding node =
            PrinterProtocol.NodeStatusChanged (node, NodeStatus.Building) |> post

        member _.NodeOutput node line =
            PrinterProtocol.NodeOutput (node, line) |> post

        member _.NodeUploading node =
            PrinterProtocol.NodeStatusChanged (node, NodeStatus.Uploading) |> post

        member _.NodeCompleted node request success =
            PrinterProtocol.NodeCompleted (node, request, success) |> post
