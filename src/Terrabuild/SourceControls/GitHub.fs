namespace SourceControls
open Environment

type GitHub() =
    let refName = "GITHUB_REF_NAME" |> envVar
    let refType = "GITHUB_REF_TYPE" |> envVar
    let stepSummary = "GITHUB_STEP_SUMMARY" |> envVar
    let runId = "GITHUB_RUN_ID" |> envVar
    let repository = "GITHUB_REPOSITORY" |> envVar
    let runAttempt = "GITHUB_RUN_ATTEMPT" |> envVar |> int
    let commitLog = currentDir() |> Git.getCommitLog
    let commit = commitLog.Head

    static member Detect() =
        "GITHUB_ACTION" |> envVar |> isNull |> not

    interface Contracts.ISourceControl with
        override _.BranchOrTag = refName
        override _.HeadCommit =
            { Sha = commit.Sha; Subject = commit.Subject; Author = commit.Author; Email = commit.Email }
        override _.CommitLog = commitLog.Tail |> List.map (fun commit -> 
            { Sha = commit.Sha; Subject = commit.Subject; Author = commit.Author; Email = commit.Email })
        override _.Run = 
            Some { Name = "GitHub"
                   IsTag = refType = "tag"
                   RunId = runId
                   Repository = repository
                   RunAttempt = runAttempt }

        override _.LogType = Contracts.LogType.Markdown stepSummary

        override _.LogError msg = $"::error::{msg}" |> Terminal.writeLine
