namespace SourceControls

type GitHub() =
    inherit SourceControl()

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
