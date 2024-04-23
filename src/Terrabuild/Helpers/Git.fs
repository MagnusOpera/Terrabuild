module Git

let getHeadCommit (dir: string) =
    match Exec.execCaptureOutput dir "git" "rev-parse HEAD" with
    | Exec.Success (output, _) -> output |> String.firstLine
    | _ -> failwith "Failed to get head commit"

let getBranch (dir: string) =
    match Exec.execCaptureOutput dir "git" "rev-parse --abbrev-ref HEAD" with
    | Exec.Success (output, _) -> output |> String.firstLine
    | _ -> failwith "Failed to get branch"
