namespace SourceControls

type GitHub() =
    static let sha = System.Environment.GetEnvironmentVariable("GITHUB_SHA")
    static let refName = System.Environment.GetEnvironmentVariable("GITHUB_REF_NAME")
    static let stepSummary = System.Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")

    static member Detect() =
        [ sha; refName; stepSummary ] |> List.forall (not << isNull)

    interface Contracts.ISourceControl with
        override _.HeadCommit = sha

        override _.BranchOrTag = refName

        override _.LogType = Contracts.LogType.Markdown stepSummary

        override _.LogError msg = $"::error::{msg}" |> Terminal.writeLine

        override _.CI = true

        override _.Name = "GitHub"
