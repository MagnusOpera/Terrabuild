namespace SourceControls
open Environment
open Environment

type GitHub() =
    let sha = "GITHUB_SHA" |> envVar
    let refName = "GITHUB_REF_NAME" |> envVar
    let stepSummary = "GITHUB_STEP_SUMMARY" |> envVar
    let repository = "GITHUB_REPOSITORY" |> envVar
    let runId = "GITHUB_RUN_ID" |> envVar
    let repository = "GITHUB_REPOSITORY" |> envVar
    let runAttempt = "GITHUB_RUN_ATTEMPT" |> envVar |> int
    let author = currentDir() |> Git.getHeadCommitAuthor

    static member Detect() =
        "GITHUB_ACTION" |> envVar |> isNull |> not

    interface Contracts.ISourceControl with
        override _.BranchOrTag = refName
        override _.HeadCommit = sha
        override _.User = author
        override _.Run = 
            Some { Name = "GitHub"
                   Message = currentDir() |> Git.getHeadCommitMessage
                   Author = currentDir() |> Git.getHeadCommitAuthor
                   LogUrl = $"https://github.com/{repository}/commit/{sha}/checks/{runId}"
                   Repository = repository
                   RunAttempt = runAttempt }

        override _.LogType = Contracts.LogType.Markdown stepSummary

        override _.LogError msg = $"::error::{msg}" |> Terminal.writeLine
