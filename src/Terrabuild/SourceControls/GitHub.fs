namespace SourceControls
open Environment

module GitHubEventReader =

    type GitHubAuthor = {
        Email: string
        Name: string
    }

    type GitHubCommit = {
        Author: GitHubAuthor
        Id: string
        Message: string
    }

    type GitHubEvent = {
        After: string
        Before: string
        Commits: GitHubCommit list
    }

    let read (filename: string) =
        let json = filename |> IO.readTextFile
        Json.Deserialize<GitHubEvent> json

    let findOtherCommits (filename: string) =
        let event = read filename
        event.Commits
        |> List.map (fun commit -> 
            { Contracts.Commit.Sha = commit.Id
              Contracts.Commit.Message = commit.Message
              Contracts.Commit.Author = commit.Author.Name
              Contracts.Commit.Email = commit.Author.Email })


type GitHub() =
    let refName = "GITHUB_REF_NAME" |> envVar
    let refType = "GITHUB_REF_TYPE" |> envVar
    let stepSummary = "GITHUB_STEP_SUMMARY" |> envVar
    let runId = "GITHUB_RUN_ID" |> envVar
    let repository = "GITHUB_REPOSITORY" |> envVar
    let runAttempt = "GITHUB_RUN_ATTEMPT" |> envVar |> int
    let commitLog = currentDir() |> Git.getCommitLog
    let commit = commitLog.Head
    let otherCommits = "GITHUB_EVENT_PATH" |> envVar |> GitHubEventReader.findOtherCommits |> List.ofSeq

    static member Detect() =
        "GITHUB_ACTION" |> envVar |> isNull |> not

    interface Contracts.ISourceControl with
        override _.BranchOrTag = refName
        
        override _.HeadCommit =
            { Sha = commit.Sha; Message = commit.Subject; Author = commit.Author; Email = commit.Email }
        
        override _.CommitLog = commitLog.Tail |> List.map (fun commit -> 
            { Sha = commit.Sha; Message = commit.Subject; Author = commit.Author; Email = commit.Email })

        override _.Run = 
            Some { Name = "GitHub"
                   IsTag = refType = "tag"
                   RunId = runId
                   OtherCommits = otherCommits
                   Repository = repository
                   RunAttempt = runAttempt }

        override _.LogType = Contracts.LogType.Markdown stepSummary

        override _.LogError msg = $"::error::{msg}" |> Terminal.writeLine
