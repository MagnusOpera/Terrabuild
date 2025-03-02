namespace SourceControls
open Environment
open Environment

type GitHub() =
    let sha = "GITHUB_SHA" |> envVar
    let refName = "GITHUB_REF_NAME" |> envVar
    let refType = "GITHUB_REF_TYPE" |> envVar
    let refTarget = "GITHUB_BASE_REF" |> envVar
    let stepSummary = "GITHUB_STEP_SUMMARY" |> envVar
    let runId = "GITHUB_RUN_ID" |> envVar
    let repository = "GITHUB_REPOSITORY" |> envVar
    let runAttempt = "GITHUB_RUN_ATTEMPT" |> envVar |> int
    let author = currentDir() |> Git.getHeadCommitAuthor
    let commitLog = currentDir() |> Git.getCommitLog

    static member Detect() =
        "GITHUB_ACTION" |> envVar |> isNull |> not

    interface Contracts.ISourceControl with
        override _.BranchOrTag = refName
        override _.IsTag = refType = "tag"
        override _.HeadCommit = sha
        override _.CommitLog = commitLog
        override _.User = author
        override _.Run = 
            Some { Name = "GitHub"
                   Message = currentDir() |> Git.getHeadCommitMessage
                   TargetBranch = refTarget |> Option.ofObj
                   Author = currentDir() |> Git.getHeadCommitAuthor
                   RunId = runId
                   Repository = repository
                   RunAttempt = runAttempt }

        override _.LogType = Contracts.LogType.Markdown stepSummary

        override _.LogError msg = $"::error::{msg}" |> Terminal.writeLine
