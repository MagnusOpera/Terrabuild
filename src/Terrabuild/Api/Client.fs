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
    let put<'req, 'resp> = request<'req, 'resp> HttpMethod.Put


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
        CI: bool
        CIName: string option
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
    type UseArtifactInput = {
        ProjectHash: string
        TargetHash: string
    }

    let startBuild headers branchOrTag commit configuration note tag targets force retry ci ciname cimetadata: StartBuildOutput =
        { StartBuildInput.BranchOrTag = branchOrTag
          StartBuildInput.Commit = commit
          StartBuildInput.Configuration = configuration
          StartBuildInput.Note = note
          StartBuildInput.Tag = tag
          StartBuildInput.Targets = targets 
          StartBuildInput.Force = force
          StartBuildInput.Retry = retry
          StartBuildInput.CI = ci
          StartBuildInput.CIName = ciname
          StartBuildInput.CIMetadata = cimetadata }
          |> Http.post headers "/builds"

    let completeBuild headers buildId success: Unit =
        { CompleteBuildInput.Success = success }
        |> Http.post headers $"/builds/{buildId}/complete"

    let useArtifact headers buildId projectHash targetHash =
        { UseArtifactInput.ProjectHash = projectHash
          UseArtifactInput.TargetHash = targetHash }
        |> Http.put<UseArtifactInput, Unit> headers $"/builds/{buildId}/use-artifact"


module private Artifact =
    [<RequireQualifiedAccess>]
    type CreateArtifactInput = {
        Project: string
        Target: string
        ProjectHash: string
        TargetHash: string
    }

    [<RequireQualifiedAccess>]
    type CompleteArtifactInput = {
        Parts: string list
        Success: bool
    }

    [<RequireQualifiedAccess>]
    type AzureArtifactLocationOutput = {
        Uri: string
    }

    let createArtifact headers project target projectHash targetHash =
        { CreateArtifactInput.ProjectHash = projectHash
          CreateArtifactInput.TargetHash = targetHash            
          CreateArtifactInput.Project = project
          CreateArtifactInput.Target = target }
        |> Http.post<CreateArtifactInput, Unit> headers $"/artifacts"

    let completeArtifact headers projectHash targetHash parts success =
        { CompleteArtifactInput.Parts = parts
          CompleteArtifactInput.Success = success }
        |> Http.put<CompleteArtifactInput, Unit> headers $"/artifacts/{projectHash}/{targetHash}/complete"

    let getArtifactPart headers projectHash targetHash part =
        Http.get<Unit, AzureArtifactLocationOutput> headers $"/artifacts/{projectHash}/{targetHash}/{part}" ()

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
        member _.StartBuild branchOrTag commit configuration note tag targets force retry ci ciname cimetadata =
            let resp = Build.startBuild headers branchOrTag commit configuration note tag targets force retry ci ciname cimetadata
            resp.BuildId

        member _.CompleteBuild buildId success =
            Build.completeBuild headers buildId success

        member _.UseArtifact buildId projectHash targetHash =
            Build.useArtifact headers buildId projectHash targetHash

        member _.GetArtifactPart projectHash targetHash part =
            let resp = Artifact.getArtifactPart headers projectHash targetHash part
            Uri(resp.Uri)

        member _.CreateArtifact project target projectHash targetHash =
            Artifact.createArtifact headers project target projectHash targetHash

        member _.CompleteArtifact projectHash targetHash parts success =
            Artifact.completeArtifact headers projectHash targetHash parts success
