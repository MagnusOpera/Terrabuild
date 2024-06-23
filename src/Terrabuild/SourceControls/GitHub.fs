namespace SourceControls

type GitHub() =
    inherit Contracts.SourceControl()

    static let sha = System.Environment.GetEnvironmentVariable("GITHUB_SHA")
    static let refName = System.Environment.GetEnvironmentVariable("GITHUB_REF_NAME")
    static let stepSummary = System.Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY")

    static member Detect() =
        [ sha; refName; stepSummary ] |> List.forall (not << isNull)

    override _.HeadCommit = sha

    override _.BranchOrTag = refName

    override _.LogType = Contracts.LogType.Markdown stepSummary

    override _.CI = true

    override _.Name = "GitHub"
