module ApiClient
open System
open FSharp.Data


let private apiUrl =
    let baseUrl = DotNetEnv.Env.GetString("TERRABUILD_API_URL", "https://api.terrabuild.io")
    Uri(baseUrl)

let inline private request<'req, 'resp> method (path: string) (request: 'req): 'resp =
    let url = Uri(apiUrl, path).ToString()
    let body =
        if typeof<'req> <> typeof<Unit> then request |> FSharpJson.Serialize |> TextRequest |> Some
        else None

    let headers = [
        HttpRequestHeaders.Accept HttpContentTypes.Json
        HttpRequestHeaders.ContentType HttpContentTypes.Json ]
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
        { AuthenticateInput.Token = token }
        |> options "/auth" 

    let loginSpace space token: LoginSpaceOutput =
        { LoginSpaceInput.Space = space
          LoginSpaceInput.Token = token }
        |> post "/auth"
