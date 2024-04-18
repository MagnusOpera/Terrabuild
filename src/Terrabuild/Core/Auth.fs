module Auth
// open FSharp.Data
open System
open System.Net.Http
open System.Text
open System.Net

[<RequireQualifiedAccess>]
type TokenInput = {
    Space: string
    Token: string
}

[<RequireQualifiedAccess>]
type LoginOutput = {
    AccessToken: string
}


let httpClient = new HttpClient()
let login space token =
    // try exchanging this token with a real token
    // if successful token is validated
    let baseUrl = DotNetEnv.Env.GetString("TERRABUILD_API_URL", "https://api.terrabuild.io")
    let url = Uri(Uri(baseUrl), "/auth").ToString()
    let webRequest = new HttpRequestMessage(HttpMethod.Patch, url)
    let request =
        { TokenInput.Space = space; TokenInput.Token = token }
        |> FSharpJson.Serialize
    webRequest.Content <- new StringContent(request, UnicodeEncoding.UTF8, "application/json")
    let webResponse = httpClient.Send(webRequest)
    if webResponse.StatusCode = HttpStatusCode.OK then
        let response =
            webResponse.Content.ReadAsStringAsync()
            |> Async.AwaitTask
            |> Async.RunSynchronously
            |> FSharpJson.Deserialize<LoginOutput>

        // token is validated, can go to disk
        let spaceAuth = {
            Cache.SpaceAuth.Url = url
            Cache.SpaceAuth.Space = space
            Cache.SpaceAuth.Token = token
        }

        Cache.addSpaceAuth spaceAuth
        0
    else
        $"{Ansi.Emojis.bomb} {webResponse.StatusCode}: please check permissions with your administrator." |> Terminal.writeLine
        5

let logout space =
    Cache.removeSpaceAuth space
