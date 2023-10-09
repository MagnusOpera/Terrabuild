module Progress
open System
open Ansi.Styles
open Ansi.Emojis

type ProgressStatus =
    | Success
    | Fail
    | Progress of startedAt:DateTime
    | Scheduled of startedAt:DateTime

type ProgressItem = {
    mutable Status: ProgressStatus
    Label: string
}

type ProgressRenderer() =
    let mutable items = []

    let spinnerWaiting = [ "◐"; "◓"; "◑"; "◒" ]
    let frequencyWaiting = 200.0

    let spinnerProgress = [ "⠋"; "⠙"; "⠹"; "⠸"; "⠼"; "⠴";  "⠦"; "⠧"; "⠇"; "⠏" ]
    let frequencyProgress = 100.0

    let printableStatus item =
        match item.Status with
        | Success -> green + checkmark + reset
        | Fail -> red + crossmark + reset
        | Scheduled startedAt ->
            let diff = ((DateTime.Now - startedAt).TotalMilliseconds / frequencyWaiting) |> int
            let offset = diff % spinnerWaiting.Length
            yellow + spinnerWaiting[offset] + reset
        | Progress startedAt ->
            let diff = ((DateTime.Now - startedAt).TotalMilliseconds / frequencyProgress) |> int
            let offset = diff % spinnerProgress.Length
            yellow + spinnerProgress[offset] + reset

    let printableItem item =
        let status = printableStatus item
        $"{status} {item.Label}"

    member _.Refresh () =
        // update status: move home, move top, write status
        let updateCmd =
            items
            |> List.fold (fun acc item -> acc + $"{Ansi.cursorHome}{Ansi.cursorUp 1}" + (item |> printableStatus)) ""
        let updateCmd = cursorHide + updateCmd + $"{Ansi.cursorHome}{Ansi.cursorDown items.Length}" + cursorShow
        updateCmd |> Console.Out.Write

    member _.Update (label: string) (status: ProgressStatus) =
        match items |> List.tryFindIndex (fun item -> item.Label = label) with
        | Some index ->
            items[index].Status <- status
        | _ ->
            let item = { Status = status; Label = label }
            items <- item :: items
            let printable = printableItem item
            Console.Out.WriteLine(printable)
