module Progress
open System
open Ansi.Styles
open Ansi.Emojis

[<RequireQualifiedAccess>]
type ProgressStatus =
    | Success
    | Fail
    | Running of startedAt:DateTime * spinner:string * frequency:double

type ProgressItem = {
    Label: string
    mutable Status: ProgressStatus
}

type ProgressRenderer() =
    let mutable items = []

    // // https://antofthy.gitlab.io/info/ascii/HeartBeats_howto.txt
    // let spinnerWaiting = [ "⠁"; "⠂"; "⠄"; "⠂" ]
    // let frequencyWaiting = 200.0

    // let spinnerUpload = [ "⣤"; "⠶"; "⠛"; "⠛"; "⠶" ]
    // let frequencyUpload = 200.0

    // let spinnerProgress = [ "⠋"; "⠙"; "⠹"; "⠸"; "⠼"; "⠴";  "⠦"; "⠧"; "⠇"; "⠏" ]
    // let frequencyProgress = 100.0

    let printableStatus item =
        match item.Status with
        | ProgressStatus.Success -> green + " " + checkmark + reset
        | ProgressStatus.Fail -> red + " " + crossmark + reset
        | ProgressStatus.Running (startedAt, spinner, frequency) ->
            let diff = ((DateTime.Now - startedAt).TotalMilliseconds / frequency) |> int
            let offset = diff % spinner.Length
            $"{yellow} {spinner[offset]}{reset}"

    let printableItem item =
        let status = printableStatus item
        $"{status} {item.Label}"

    member _.Refresh () =
        // update status: move home, move top, write status
        let updateCmd =
            items
            |> List.fold (fun acc item -> acc + $"{Ansi.cursorHome}{Ansi.cursorUp 1}" + (item |> printableStatus)) ""
        let updateCmd = updateCmd + $"{Ansi.cursorHome}{Ansi.cursorDown items.Length}"
        updateCmd |> Terminal.write |> Terminal.flush

    member _.Update (label: string) (spinner: string) (frequency: double) =
        match items |> List.tryFindIndex (fun item -> item.Label = label) with
        | Some index ->
            items[index].Status <- ProgressStatus.Running (DateTime.Now, spinner, frequency)
        | _ ->
            let item = { Label = label; Status = ProgressStatus.Running (DateTime.Now, spinner, frequency) }
            items <- item :: items
            printableItem item |> Terminal.writeLine |> Terminal.flush

    member _.Complete (label: string) (success: bool) =
        let status =
            if success then ProgressStatus.Success
            else ProgressStatus.Fail

        match items |> List.tryFindIndex (fun item -> item.Label = label) with
        | Some index ->
            items[index].Status <- status
        | _ ->
            let item = { Label = label; Status = status }
            items <- item :: items
