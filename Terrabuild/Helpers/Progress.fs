module Progress
open System
open Ansi
open Ansi.Styles
open Ansi.Emojis

type ProgressStatus =
    | Success
    | Fail
    | Progress of startedAt:DateTime

type ProgressItem = {
    mutable Status: ProgressStatus
    Label: string
}

type ProgressRenderer() =
    let mutable items = []

    let spinner = [ "⠋"; "⠙"; "⠹"; "⠸"; "⠼"; "⠴";  "⠦"; "⠧"; "⠇"; "⠏" ]
    let frequency = 100.0

    let printableStatus item =
        match item.Status with
        | Success -> green + checkmark + reset
        | Fail -> red + crossmark + reset
        | Progress startedAt ->
            let diff = ((DateTime.Now - startedAt).TotalMilliseconds / frequency) |> int
            let offset = diff % spinner.Length
            yellow + spinner[offset] + reset

    let printableItem item =
        let status = printableStatus item
        $"{status} {item.Label}"

    member _.Refresh () =
        // update status: move home, move top, write status
        let updateCmd =
            items
            |> List.fold (fun acc item -> acc + $"{Ansi.cursorHome}{Ansi.cursorUp 1}" + (item |> printableStatus)) ""
        let updateCmd = updateCmd + $"{Ansi.cursorHome}{Ansi.cursorDown items.Length}"
        updateCmd |> Console.Out.Write

    member _.Add (label: string) (status: ProgressStatus) =
        let item = { Status = status; Label = label }
        items <- item :: items
        let printable = printableItem item
        Console.Out.WriteLine(printable)

    member this.Update (label: string) (status: ProgressStatus) =
        let index = items |> List.findIndex (fun item -> item.Label = label)
        items[index].Status <- status
        this.Refresh()
