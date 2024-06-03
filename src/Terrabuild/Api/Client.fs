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
            if typeof<'req> <> typeof<Unit> then request |> FSharpJson.Serialize |> TextRequest |> Some
            else None

        let response = Http.RequestString(url = url, headers = headers, ?body = body, httpMethod = method)

        if typeof<'resp> <> typeof<Unit> then response |> FSharpJson.Deserialize<'resp>
        else Unchecked.defaultof<'resp>

    let inline options<'req, 'resp> = request<'req, 'resp> HttpMethod.Options
    let inline get<'req, 'resp> = request<'req, 'resp> HttpMethod.Get
    let inline post<'req, 'resp> = request<'req, 'resp> HttpMethod.Post


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
        Targets: string seq
        Force: bool
        Retry: bool
        CI: bool
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

    let startBuild headers branchOrTag commit configuration targets force retry ci: StartBuildOutput =
        { StartBuildInput.BranchOrTag = branchOrTag
          StartBuildInput.Commit = commit
          StartBuildInput.Configuration = configuration
          StartBuildInput.Targets = targets 
          StartBuildInput.Force = force
          StartBuildInput.Retry = retry
          StartBuildInput.CI = ci }
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
        member _.BuildStart branchOrTag commit configuration targets force retry ci =
            let resp = Build.startBuild headers branchOrTag commit configuration targets force retry ci
            resp.BuildId

        member _.BuildComplete buildId success =
            Build.completeBuild headers buildId success

        member _.BuildAddArtifact buildId project target projectHash hash files size success =
            Build.addArtifact headers buildId project target projectHash hash files size success

        member _.ArtifactGet path =
            let resp = Artifact.getArtifact headers path
            Uri(resp.Uri)
