namespace SourceControls

type GitHub() =
    static let sha = System.Environment.GetEnvironmentVariable("GITHUB_SHA")
    static let refName = System.Environment.GetEnvironmentVariable("GITHUB_REF_NAME")
    static let stepSummary = System.Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")
    static let repository = System.Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")
    static let runId = System.Environment.GetEnvironmentVariable("GITHUB_RUN_ID")


    static member Detect() =
        [ sha; refName; stepSummary; repository; runId ] |> List.forall (not << isNull)

    interface Contracts.ISourceControl with
        override _.HeadCommit = sha

        override _.BranchOrTag = refName

        override _.LogType = Contracts.LogType.Markdown stepSummary

        override _.LogError msg = $"::error::{msg}" |> Terminal.writeLine

        override _.CI = Some "GitHub"

        override _.Metadata = Some $"https://github.com/{repository}/commit/{sha}/checks/{runId}"
