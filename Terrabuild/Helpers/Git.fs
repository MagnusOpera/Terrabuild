module Helpers.Git

let listFiles (dir: string) =
    match Exec.execCaptureOutput dir "git" "ls-tree -rl HEAD ." with
    | Exec.Success (listing, _) -> listing
    | _ -> failwith "Failed to get listing"


let getHeadCommit (dir: string) =
    match Exec.execCaptureOutput dir "git" "rev-parse HEAD" with
    | Exec.Success (output, _) -> output
    | _ -> failwith "Failed to get listing"
