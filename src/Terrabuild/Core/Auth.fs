module Auth
// open FSharp.Data
open System
open System.Net.Http
open System.Text
open System.Net
open FSharp.Data

[<RequireQualifiedAccess>]
type CheckTokenInput = {
    Token: string
}

let httpClient = new HttpClient()
let login token =
    // try exchanging this token with a real token
    // if successful token is validated
    try
        let baseUrl = DotNetEnv.Env.GetString("TERRABUILD_API_URL", "https://api.terrabuild.io")
        let url = Uri(Uri(baseUrl), "/auth").ToString()
        let request =
            { CheckTokenInput.Token = token }
            |> FSharpJson.Serialize
        let headers = [
            HttpRequestHeaders.Accept HttpContentTypes.Json
            HttpRequestHeaders.ContentType HttpContentTypes.Json ]
        let response = Http.Request(url = url, headers = headers, body = TextRequest request, httpMethod = HttpMethod.Options)

        // token is validated, can go to disk
        Cache.addAuthToken token
        0
    with
        | :? WebException as ex ->
            let errorCode =
                match ex.InnerException with
                | :? WebException as innerEx ->
                    match innerEx.Response with
                    | :? HttpWebResponse as hwr -> hwr.StatusCode.ToString()
                    | _ -> ex.Message
                | _ -> ex.Message

            $"{Ansi.Emojis.bomb} {errorCode}: please check permissions with your administrator." |> Terminal.writeLine
            5

let logout () =
    Cache.removeAuthToken()
