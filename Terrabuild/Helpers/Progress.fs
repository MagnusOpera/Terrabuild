module Progress
open System

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

    let crossmark = "✘"
    let checkmark = "✔"
    let ESC = "\u001b"
    let CSI = ESC + "["
    let green = $"{CSI}32m"
    let red = $"{CSI}31m"
    let yellow = $"{CSI}33m"
    let normal = $"{CSI}0m"

    let printableStatus item =
        match item.Status with
        | Success -> green + checkmark + normal
        | Fail -> red + crossmark + normal
        | Progress startedAt ->
            let diff = ((DateTime.Now - startedAt).TotalMilliseconds / frequency) |> int
            let offset = diff % spinner.Length
            yellow + spinner[offset] + normal

    let printableItem item =
        let status = printableStatus item
        $"{status} {item.Label}"

    member _.Refresh () =
        // update status: move home, move top, write status
        let updateCmd =
            items
            |> List.fold (fun acc item -> acc + $"\r{CSI}1A" + (item |> printableStatus)) ""
        let updateCmd = updateCmd + $"\r{CSI}{items.Length}B"
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
