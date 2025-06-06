module Git
open Errors
open System

let getBranchOrTag (dir: string) =
    // https://stackoverflow.com/questions/18659425/get-git-current-branch-tag-name
    match Exec.execCaptureOutput dir "git" "symbolic-ref -q --short HEAD" with
    | Exec.Success (output, _) -> output |> String.firstLine
    | _ -> 
        match Exec.execCaptureOutput dir "git" "describe --tags --exact-match" with
        | Exec.Success (output, _) -> output |> String.firstLine
        | _ -> raiseExternalError "Failed to get branch or tag"

let getHeadCommitMessage (dir: string) =
    match Exec.execCaptureOutput dir "git" "log -1 --pretty=%B" with
    | Exec.Success (output, _) -> output
    | _ -> raiseExternalError "Failed to get head commit message"

let getCurrentUser (dir: string) =
    match Exec.execCaptureOutput dir "git" "config user.name" with
    | Exec.Success (output, _) -> output |> String.firstLine
    | _ -> raiseExternalError "Failed to get head commit"

let getCommitLog (dir: string) =
    match Exec.execCaptureOutput dir "git" "log -n 10 --pretty=%H%n%s%n%an%n%ae%n%aI" with
    | Exec.Success (output, _) ->
        output |> String.getLines
        |> Seq.chunkBySize 5
        |> Seq.map (fun arr -> {| Sha = arr[0]; Subject = arr[1]; Author = arr[2]; Email = arr[3]; Timestamp = DateTime.Parse(arr[4]) |})
        |> List.ofSeq
    | _ -> raiseExternalError "Failed to get commit log"
