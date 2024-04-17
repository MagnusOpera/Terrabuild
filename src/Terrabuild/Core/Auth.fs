module Auth
// open FSharp.Data
open System
open System.Net.Http
open System.Text

[<RequireQualifiedAccess>]
type TokenInput = {
    Token: string
}

type LoginOutput = {
    AccessToken: string
}


let httpClient = new HttpClient()
let loginToken token =

    // try exchanging this token with a real token
    // if successful token is validated
    let baseUrl = DotNetEnv.Env.GetString("TERRABUILD_API_URL", "https://api.terrabuild.io")
    let url = Uri(Uri(baseUrl), "/auth").ToString()
    let webRequest = new HttpRequestMessage(HttpMethod.Patch, url)
    let request =
        { TokenInput.Token = token }
        |> FSharpJson.Serialize
    webRequest.Content <- new StringContent(request, UnicodeEncoding.UTF8, "application/json")
    let webResponse = httpClient.Send(webRequest)
    let response =
        webResponse.Content.ReadAsStringAsync()
        |> Async.AwaitTask
        |> Async.RunSynchronously
        |> FSharpJson.Deserialize<LoginOutput>

    // token is validated, can go to disk
    let config = {
        Cache.Configuration.Url = url
        Cache.Configuration.Token = response.AccessToken
    }

    Cache.writeConfig config
