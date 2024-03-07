namespace SourceControls

type GitHub() =
    inherit SourceControl()

    static member Detect() =
        System.Environment.GetEnvironmentVariable("GITHUB_SHA") |> isNull |> not
        && System.Environment.GetEnvironmentVariable("GITHUB_REF_NAME") |> isNull |> not

    override _.HeadCommit =
        let hash = System.Environment.GetEnvironmentVariable("GITHUB_SHA")
        if hash |> isNull then
            failwith "Environment variable GITHUB_SHA not found"
        hash

    override _.BranchOrTag =
        let branchOrRef = System.Environment.GetEnvironmentVariable("GITHUB_REF_NAME")
        if branchOrRef |> isNull then
            failwith "Environment variable GITHUB_REF_NAME not found"
        branchOrRef

    override _.CI = true

    override _.Name = "GitHub"
