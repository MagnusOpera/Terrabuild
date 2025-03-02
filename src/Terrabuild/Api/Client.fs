namespace Api
open System
open FSharp.Data
open Collections


module private Http =
    open Serilog
    open System.Net
    open Errors
    let apiUrl =
        let baseUrl = DotNetEnv.Env.GetString("TERRABUILD_API_URL", "https://api.prod.magnusopera.io/terrabuild")
        Uri(baseUrl)

    let private request<'req, 'resp> method headers (path: string) (request: 'req): 'resp =
        let url = Uri($"{apiUrl}{path}").ToString()
        let body =
            if typeof<'req> <> typeof<Unit> then request |> Json.Serialize |> TextRequest |> Some
            else None

        try
            let response = Http.RequestString(url = url, headers = headers, ?body = body, httpMethod = method)

            if typeof<'resp> <> typeof<Unit> then response |> Json.Deserialize<'resp>
            else Unchecked.defaultof<'resp>

        with
        | exn ->
            Log.Fatal(exn, "API error: {method} {url} with content {body}", method, url, body)

            let errorCode =
                match exn.InnerException with
                | :? WebException as innerEx ->
                    match innerEx.Response with
                    | :? HttpWebResponse as hwr -> hwr.StatusCode.ToString()
                    | _ -> exn.Message
                | _ -> exn.Message

            if errorCode = "422" then
                TerrabuildException.Raise($"Storage limit exceeded, please check your subscription.", exn)
            else
                TerrabuildException.Raise($"Api failed with error {errorCode}.", exn)

    let get<'req, 'resp> = request<'req, 'resp> HttpMethod.Get
    let post<'req, 'resp> = request<'req, 'resp> HttpMethod.Post


module private Auth =
    [<RequireQualifiedAccess>]
    type LoginSpaceInput = {
        Id: string
        Token: string
    }

    [<RequireQualifiedAccess>]
    type LoginSpaceOutput = {
        AccessToken: string
    }

    let loginSpace headers workspaceId token: LoginSpaceOutput =
        { LoginSpaceInput.Id = workspaceId
          LoginSpaceInput.Token = token }
        |> Http.post headers "/auth/login"


module private Build =

    [<RequireQualifiedAccess>]
    type RunInfoInput = {
        Name: string
        Repository: string
        Message: string
        Author: string
        Id: string
        Attempt: int
    }

    [<RequireQualifiedAccess>]
    type BuildContextInput = {
        Configuration: string
        Note: string option
        Tag: string option
        Targets: string seq
        Force: bool
        Retry: bool
    }

    [<RequireQualifiedAccess>]
    type StartBuildInput = {
        BranchOrTag: string
        Commit: string
        CommitLog: string seq
        User: string
        Run: RunInfoInput option
        Context: BuildContextInput
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
        TargetHash: string
        Files: string list
        Success: bool
    }

    [<RequireQualifiedAccess>]
    type UseArtifactInput = {
        ProjectHash: string
        TargetHash: string
    }

    let startBuild headers branchOrTag headCommit commitLog user run context : StartBuildOutput =
        { StartBuildInput.BranchOrTag = branchOrTag
          StartBuildInput.Commit = headCommit
          StartBuildInput.CommitLog = commitLog
          StartBuildInput.User = user
          StartBuildInput.Run = run
          StartBuildInput.Context = context }
        |> Http.post headers "/builds"


    let addArtifact headers buildId project target projectHash targetHash files success: Unit =
        { AddArtifactInput.Project = project
          AddArtifactInput.Target = target
          AddArtifactInput.ProjectHash = projectHash
          AddArtifactInput.TargetHash = targetHash
          AddArtifactInput.Files = files
          AddArtifactInput.Success = success }
        |> Http.post<AddArtifactInput, Unit> headers $"/builds/{buildId}/add-artifact"

    let useArtifact headers buildId projectHash hash: Unit =
        { UseArtifactInput.ProjectHash = projectHash
          UseArtifactInput.TargetHash = hash }
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


type Client(workspaceId: string, token: string, options: ConfigOptions.Options) =
    let accesstoken =
        let headers = [
            HttpRequestHeaders.Accept HttpContentTypes.Json
            HttpRequestHeaders.ContentType HttpContentTypes.Json
        ]
        let resp = Auth.loginSpace headers workspaceId token
        resp.AccessToken

    let headers = [
        HttpRequestHeaders.Accept HttpContentTypes.Json
        HttpRequestHeaders.ContentType HttpContentTypes.Json
        HttpRequestHeaders.Authorization $"Bearer {accesstoken}" ]

    let buildId =
        lazy(
            let run = options.Run |> Option.map (fun run -> {
                Build.RunInfoInput.Name = run.Name
                Build.RunInfoInput.Repository = run.Repository
                Build.RunInfoInput.Id = run.RunId
                Build.RunInfoInput.Message = run.Message
                Build.RunInfoInput.Author = run.Author
                Build.RunInfoInput.Attempt = run.RunAttempt
            })

            let context = {
                Build.BuildContextInput.Configuration = options.Configuration
                Build.BuildContextInput.Note = options.Note
                Build.BuildContextInput.Tag = options.Tag
                Build.BuildContextInput.Targets = options.Targets
                Build.BuildContextInput.Force = options.Force
                Build.BuildContextInput.Retry = options.Retry }

            let resp = Build.startBuild headers
                                        options.BranchOrTag
                                        options.HeadCommit
                                        options.CommitLog
                                        options.User
                                        run
                                        context
            resp.BuildId)

    interface Contracts.IApiClient with
        member _.StartBuild () =
            buildId.Force() |> ignore

        member _.CompleteBuild success =
            Build.completeBuild headers buildId success

        member _.AddArtifact project target projectHash targetHash files success =
            Build.addArtifact headers buildId project target projectHash targetHash files success

        member _.UseArtifact projectHash hash =
            Build.useArtifact headers buildId projectHash hash

        member _.GetArtifact path =
            let resp = Artifact.getArtifact headers path
            Uri(resp.Uri)
