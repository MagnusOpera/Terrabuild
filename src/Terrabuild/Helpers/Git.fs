module Git

let getHeadCommit (dir: string) =
    match Exec.execCaptureOutput dir "git" "rev-parse HEAD" with
    | Exec.Success (output, _) -> output |> String.firstLine
    | _ -> failwith "Failed to get head commit"

let getBranchOrTag (dir: string) =
    // https://stackoverflow.com/questions/18659425/get-git-current-branch-tag-name
    match Exec.execCaptureOutput dir "git" "symbolic-ref -q --short HEAD" with
    | Exec.Success (output, _) -> output |> String.firstLine
    | _ -> 
        match Exec.execCaptureOutput dir "git" "describe --tags --exact-match" with
        | Exec.Success (output, _) -> output |> String.firstLine
        | _ -> failwith "Failed to get branch or tag"
