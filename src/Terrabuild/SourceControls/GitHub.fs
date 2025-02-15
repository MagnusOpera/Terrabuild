namespace SourceControls

type GitHub() =
    let sha = System.Environment.GetEnvironmentVariable("GITHUB_SHA")
    let refName = System.Environment.GetEnvironmentVariable("GITHUB_REF_NAME")
    let stepSummary = System.Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")
    let repository = System.Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")
    let runId = System.Environment.GetEnvironmentVariable("GITHUB_RUN_ID")
    let repository = System.Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")
    let runAttempt = System.Environment.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT") |> int
    let author = System.Environment.CurrentDirectory |> Git.getHeadCommitAuthor

    static member Detect() =
        System.Environment.GetEnvironmentVariable("GITHUB_ACTION") |> isNull |> not

    interface Contracts.ISourceControl with
        override _.BranchOrTag = refName
        override _.HeadCommit = sha
        override _.User = author
        override _.Run = 
            Some { Name = "GitHub"
                   Message = System.Environment.CurrentDirectory |> Git.getHeadCommitMessage
                   Author = System.Environment.CurrentDirectory |> Git.getHeadCommitAuthor
                   LogUrl = $"https://github.com/{repository}/commit/{sha}/checks/{runId}"
                   Repository = repository
                   RunAttempt = runAttempt }

        override _.LogType = Contracts.LogType.Markdown stepSummary

        override _.LogError msg = $"::error::{msg}" |> Terminal.writeLine
