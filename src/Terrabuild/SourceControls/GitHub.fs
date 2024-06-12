namespace SourceControls
open Errors

type GitHub() =
    inherit Contracts.SourceControl()

    static member Detect() =
        System.Environment.GetEnvironmentVariable("GITHUB_SHA") |> isNull |> not
        && System.Environment.GetEnvironmentVariable("GITHUB_REF_NAME") |> isNull |> not

    override _.HeadCommit =
        let hash = System.Environment.GetEnvironmentVariable("GITHUB_SHA")
        hash

    override _.BranchOrTag =
        let branchOrRef = System.Environment.GetEnvironmentVariable("GITHUB_REF_NAME")
        branchOrRef

    override _.CI = true

    override _.Name = "GitHub"

    override _.Log success title =
        let errMsg =
            match success with
            | true -> "::error::{title} failed\n"
            | _ -> ""
        $"{errMsg}::group::{title}", "::endgroup::"
