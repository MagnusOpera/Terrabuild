module ApiClient
open System
open FSharp.Data
open System.Net
open Collections


let private apiUrl =
    let baseUrl = DotNetEnv.Env.GetString("TERRABUILD_API_URL", "https://api.terrabuild.io")
    Uri(baseUrl)

let inline private request<'req, 'resp> method headers (path: string) (request: 'req): 'resp =
    let url = Uri(apiUrl, path).ToString()
    let body =
        if typeof<'req> <> typeof<Unit> then request |> FSharpJson.Serialize |> TextRequest |> Some
        else None

    let response = Http.RequestString(url = url, headers = headers, ?body = body, httpMethod = method)

    if typeof<'resp> <> typeof<Unit> then response |> FSharpJson.Deserialize<'resp>
    else Unchecked.defaultof<'resp>

let inline private options<'req, 'resp> = request<'req, 'resp> HttpMethod.Options
let inline private get<'req, 'resp> = request<'req, 'resp> HttpMethod.Get
let inline private post<'req, 'resp> = request<'req, 'resp> HttpMethod.Post


module Auth =
    [<RequireQualifiedAccess>]
    type AuthenticateInput = {
        Token: string
    }

    [<RequireQualifiedAccess>]
    type LoginSpaceInput = {
        Space: string
        Token: string
    }

    [<RequireQualifiedAccess>]
    type LoginSpaceOutput = {
        AccessToken: string
    }

    let authenticate token: Unit =
        let headers = [
            HttpRequestHeaders.Accept HttpContentTypes.Json
            HttpRequestHeaders.ContentType HttpContentTypes.Json
        ]

        { AuthenticateInput.Token = token }
        |> options headers "/auth" 

    let loginSpace headers token space: LoginSpaceOutput =
        { LoginSpaceInput.Token = token
          LoginSpaceInput.Space = space }
        |> post headers "/auth"


module Build =
    [<RequireQualifiedAccess>]
    type StartBuildInput = {
        BranchOrTag: string
        Commit: string
        Targets: string set
        TriggeredBy: string
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
        Files: string list
        Size: int
        Success: bool
    }

    let startBuild headers branchOrTag commit targets triggeredBy: StartBuildOutput =
        { StartBuildInput.BranchOrTag = branchOrTag
          StartBuildInput.Commit = commit
          StartBuildInput.Targets = targets
          StartBuildInput.TriggeredBy = triggeredBy }
          |> post headers "/build"


    let addArtifact headers buildId project target files size success: Unit =
        { AddArtifactInput.Project = project
          AddArtifactInput.Target = target
          AddArtifactInput.Files = files
          AddArtifactInput.Size = size
          AddArtifactInput.Success = success }
        |> post<AddArtifactInput, Unit> headers $"/build/{buildId}/add-artifact"


    let completeBuild headers buildId success: Unit =
        { CompleteBuildInput.Success = success }
        |> post headers $"/build/{buildId}/complete"


module Artifact =
    [<RequireQualifiedAccess>]
    type AzureArtifactLocationOutput = {
        Uri: string
    }

    let getArtifact headers path: AzureArtifactLocationOutput =
        get<Unit, AzureArtifactLocationOutput> headers $"/artifact?path={path}" ()


type IClient =
    abstract BuildStart: branchOrTag:string -> commit:string -> targets:string set -> triggeredBy:string -> string
    abstract BuildComplete: buildId:string -> success:bool -> Unit
    abstract BuildAddArtifact: buildId:string -> project:string -> target:string -> files:string list -> size:int -> success:bool -> Unit
    abstract ArtifactGet: path:string -> Uri

type Client(token: string, space: string) =
    let accesstoken =
        let headers = [
            HttpRequestHeaders.Accept HttpContentTypes.Json
            HttpRequestHeaders.ContentType HttpContentTypes.Json
        ]
        let resp = Auth.loginSpace headers token space
        resp.AccessToken

    let headers = [
        HttpRequestHeaders.Accept HttpContentTypes.Json
        HttpRequestHeaders.ContentType HttpContentTypes.Json
        HttpRequestHeaders.Authorization $"Bearer {accesstoken}" ]

    interface IClient with
        member _.BuildStart branchOrTag commit targets triggeredBy =
            let resp = Build.startBuild headers branchOrTag commit targets triggeredBy
            resp.BuildId

        member _.BuildComplete buildId success =
            Build.completeBuild headers buildId success

        member _.BuildAddArtifact buildId project target files size success =
            Build.addArtifact headers buildId project target files size success

        member _.ArtifactGet path =
            let resp = Artifact.getArtifact headers path
            Uri(resp.Uri)


type Null() = 
    interface IClient with
        member _.BuildStart branchOrTag commit targets triggeredBy = ""
        member _.BuildComplete buildId success = ()
        member _.BuildAddArtifact buildId project target path size success = ()
        member _.ArtifactGet path = Uri("")


let create space token =
    match token, space with
    | Some token , Some space -> 
        try
            let api: IClient = Client(space, token)
            Some api
        with
            | :? WebException as ex ->
                let errorCode =
                    match ex.InnerException with
                    | :? WebException as innerEx ->
                        match innerEx.Response with
                        | :? HttpWebResponse as hwr -> hwr.StatusCode.ToString()
                        | _ -> ex.Message
                    | _ -> ex.Message

                failwith $"{Ansi.Emojis.bomb} {errorCode}: please check permissions with your administrator to access space {space}."
    | _ ->
        None
