module Helpers.Git

let listFiles (dir: string) =
    match Exec.execCaptureOutput dir "git" "ls-tree -rl HEAD ." with
    | Exec.Success (listing, _) -> listing
    | _ -> failwith "Failed to get listing"

let listChanges (dir: string) =
    let workingPatch =
        match Exec.execCaptureOutput dir $"git" "diff --patch -- ." with
        | Exec.Success (patch, _) -> patch
        | _ -> failwith "Failed to get working patches"

    let indexPatch =
        match Exec.execCaptureOutput dir $"git" "diff --cached --patch -- ." with
        | Exec.Success (patch, _) -> patch
        | _ -> failwith "Failed to get working patches"

    String.join "\n" [ workingPatch; indexPatch ]

let getHeadCommit (dir: string) =
    match Exec.execCaptureOutput dir "git" "rev-parse HEAD" with
    | Exec.Success (output, _) -> output
    | _ -> failwith "Failed to get listing"
