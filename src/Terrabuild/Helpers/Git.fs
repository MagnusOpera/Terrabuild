module Git
open Errors

let getHeadCommit (dir: string) =
    match Exec.execCaptureOutput dir "git" "rev-parse HEAD" with
    | Exec.Success (output, _) -> output |> String.firstLine
    | _ -> TerrabuildException.Raise("Failed to get head commit")

let getBranchOrTag (dir: string) =
    // https://stackoverflow.com/questions/18659425/get-git-current-branch-tag-name
    match Exec.execCaptureOutput dir "git" "symbolic-ref -q --short HEAD" with
    | Exec.Success (output, _) -> output |> String.firstLine
    | _ -> 
        match Exec.execCaptureOutput dir "git" "describe --tags --exact-match" with
        | Exec.Success (output, _) -> output |> String.firstLine
        | _ -> TerrabuildException.Raise("Failed to get branch or tag")

let getHeadCommitMessage (dir: string) =
    match Exec.execCaptureOutput dir "git" "log -1 --pretty=%B" with
    | Exec.Success (output, _) -> output
    | _ -> TerrabuildException.Raise("Failed to get head commit message")

let getCurrentUser (dir: string) =
    match Exec.execCaptureOutput dir "git" "config user.name" with
    | Exec.Success (output, _) -> output |> String.firstLine
    | _ -> TerrabuildException.Raise("Failed to get head commit")

let getHeadCommitAuthor (dir: string) =
    match Exec.execCaptureOutput dir "git" "log -1 --pretty=%an" with
    | Exec.Success (output, _) -> output |> String.firstLine
    | _ -> TerrabuildException.Raise("Failed to get head commit")
