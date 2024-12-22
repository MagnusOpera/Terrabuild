module Notification
open System.Threading
open System.Collections.Concurrent
open Spectre.Console
open Spectre.Console.Rendering
open System.Collections.Generic

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


type private NodeInfo(title: string) =
    let nodeTitle = Markup($"[bold yellow]▶ {title}[/]")
    let tableColumn = TableColumn(nodeTitle)
    let table = Table().AddColumn(tableColumn).NoBorder()
    let grid = Grid().AddColumn()

    do
        table.AddRow(grid) |> ignore

    let renderable: IRenderable = table
    interface IRenderable with        
        member _.Measure(options, maxWidth) = renderable.Measure(options, maxWidth)
        member _.Render(options, maxWidth) = renderable.Render(options, maxWidth)

    member _.SetTitle(newTitle: string) =
        nodeTitle.Update(newTitle) |> ignore

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
        let table =
            Table()
                .AddColumn("Output")
                .HideHeaders()
                .Border(TableBorder.Horizontal)
                .ShowRowSeparators()
                .Expand()
                // .NoBorder()

        let mutable nodeInfos = Map.empty
        let updateNode node (content: string) =
            let nodeGrid =
                match nodeInfos |> Map.tryFind node with
                | Some nodeInfo -> nodeInfo
                | _ ->
                    let nodeInfo = NodeInfo(node)
                    table.AddRow(nodeInfo) |> ignore
                    nodeInfos <- nodeInfos |> Map.add node nodeInfo
                    nodeInfo
            nodeGrid.AddRow(content) |> ignore

        let updateNodeTitle node title =
            match nodeInfos |> Map.tryFind node with
            | Some nodeInfo -> nodeInfo.SetTitle(title)
            | _ -> ()

        let rec processMessages (ctx: LiveDisplayContext) =
            let mutable continueProcessing = true
            queueTrigger.WaitOne() |> ignore

            let rec dequeueAll() =
                match queue.TryDequeue() with
                | true, msg ->
                    match msg with
                    | PrinterProtocol.BuildStarted graph -> ()

                    | PrinterProtocol.BuildCompleted summary ->
                        let color =
                            match summary.IsSuccess with
                            | true -> Color.Green
                            | false -> Color.Red
                        AnsiConsole.MarkupLine($"[bold {color}]Build completed[/]")
                        buildComplete.Set() |> ignore

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
                    dequeueAll()
                | _ -> ()

            dequeueAll()
            ctx.Refresh()
            if continueProcessing then processMessages ctx

        AnsiConsole.MarkupLine("[bold green]Build started[/]")
        AnsiConsole.Live(table).Start(processMessages)


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
