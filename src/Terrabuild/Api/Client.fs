namespace Api
open System
open FSharp.Data
open Collections


module private Http =
    let apiUrl =
        let baseUrl = DotNetEnv.Env.GetString("TERRABUILD_API_URL", "https://api.terrabuild.io")
        Uri(baseUrl)

    let private request<'req, 'resp> method headers (path: string) (request: 'req): 'resp =
        let url = Uri(apiUrl, path).ToString()
        let body =
            if typeof<'req> <> typeof<Unit> then request |> Json.Serialize |> TextRequest |> Some
            else None

        let response = Http.RequestString(url = url, headers = headers, ?body = body, httpMethod = method)

        if typeof<'resp> <> typeof<Unit> then response |> Json.Deserialize<'resp>
        else Unchecked.defaultof<'resp>

    let get<'req, 'resp> = request<'req, 'resp> HttpMethod.Get
    let post<'req, 'resp> = request<'req, 'resp> HttpMethod.Post


module private Auth =
    [<RequireQualifiedAccess>]
    type LoginSpaceInput = {
        Space: string
        Token: string
    }

    [<RequireQualifiedAccess>]
    type LoginSpaceOutput = {
        AccessToken: string
    }

    let loginSpace headers space token: LoginSpaceOutput =
        { LoginSpaceInput.Space = space
          LoginSpaceInput.Token = token }
        |> Http.post headers "/auth/loginspace"


module private Build =
    [<RequireQualifiedAccess>]
    type StartBuildInput = {
        BranchOrTag: string
        Commit: string
        Configuration: string
        Note: string option
        Tag: string option
        Targets: string seq
        Force: bool
        Retry: bool
        CI: string option
        CIMetadata: string option
    }

    [<RequireQualifiedAccess>]
    type StartBuildOutput = {
        BuildId: string
    }

    [<RequireQualifiedAccess>]
    type CompleteBuildInput = {
        Success: bool
    }

    [<RequireQualifiedAccess>]
    type AddArtifactInput = {
        Project: string
        Target: string
        ProjectHash: string
        Hash: string
        Files: string list
        Size: int
        Success: bool
    }

    [<RequireQualifiedAccess>]
    type UseArtifactInput = {
        ProjectHash: string
        Hash: string
    }

    let startBuild headers branchOrTag commit configuration note tag targets force retry ci cimetadata: StartBuildOutput =
        { StartBuildInput.BranchOrTag = branchOrTag
          StartBuildInput.Commit = commit
          StartBuildInput.Configuration = configuration
          StartBuildInput.Note = note
          StartBuildInput.Tag = tag
          StartBuildInput.Targets = targets 
          StartBuildInput.Force = force
          StartBuildInput.Retry = retry
          StartBuildInput.CI = ci
          StartBuildInput.CIMetadata = cimetadata }
          |> Http.post headers "/builds"


    let addArtifact headers buildId project target projectHash hash files size success: Unit =
        { AddArtifactInput.Project = project
          AddArtifactInput.Target = target
          AddArtifactInput.ProjectHash = projectHash
          AddArtifactInput.Hash = hash
          AddArtifactInput.Files = files
          AddArtifactInput.Size = size
          AddArtifactInput.Success = success }
        |> Http.post<AddArtifactInput, Unit> headers $"/builds/{buildId}/add-artifact"

    let useArtifact headers buildId projectHash hash: Unit =
        { UseArtifactInput.ProjectHash = projectHash
          UseArtifactInput.Hash = hash }
        |> Http.post<UseArtifactInput, Unit> headers $"/builds/{buildId}/use-artifact"


    let completeBuild headers buildId success: Unit =
        { CompleteBuildInput.Success = success }
        |> Http.post headers $"/builds/{buildId}/complete"


module private Artifact =
    [<RequireQualifiedAccess>]
    type AzureArtifactLocationOutput = {
        Uri: string
    }

    let getArtifact headers path: AzureArtifactLocationOutput =
        Http.get<Unit, AzureArtifactLocationOutput> headers $"/artifacts?path={path}" ()


type Client(space: string, token: string) =
    let accesstoken =
        let headers = [
            HttpRequestHeaders.Accept HttpContentTypes.Json
            HttpRequestHeaders.ContentType HttpContentTypes.Json
        ]
        let resp = Auth.loginSpace headers space token
        resp.AccessToken

    let headers = [
        HttpRequestHeaders.Accept HttpContentTypes.Json
        HttpRequestHeaders.ContentType HttpContentTypes.Json
        HttpRequestHeaders.Authorization $"Bearer {accesstoken}" ]

    interface Contracts.IApiClient with
        member _.BuildStart branchOrTag commit configuration note tag targets force retry ci cimetadata =
            let resp = Build.startBuild headers branchOrTag commit configuration note tag targets force retry ci cimetadata
            resp.BuildId

        member _.BuildComplete buildId success =
            Build.completeBuild headers buildId success

        member _.BuildAddArtifact buildId project target projectHash hash files size success =
            Build.addArtifact headers buildId project target projectHash hash files size success

        member _.BuildUseArtifact buildId projectHash hash =
            Build.useArtifact headers buildId projectHash hash

        member _.ArtifactGet path =
            let resp = Artifact.getArtifact headers path
            Uri(resp.Uri)
