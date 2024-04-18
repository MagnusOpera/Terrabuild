module Auth
// open FSharp.Data
open System
open System.Net.Http
open System.Text
open System.Net
open FSharp.Data

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
    try
        let baseUrl = DotNetEnv.Env.GetString("TERRABUILD_API_URL", "https://api.terrabuild.io")
        let url = Uri(Uri(baseUrl), "/auth").ToString()
        let request =
            { TokenInput.Space = space; TokenInput.Token = token }
            |> FSharpJson.Serialize
        let headers = [ 
            "Accept", "application/json"
            "Content-Type", "application/json"]
        let response =
            Http.RequestString(url = url, headers = headers, body = TextRequest request, httpMethod = HttpMethod.Patch)
            |> FSharpJson.Deserialize<LoginOutput>

        // token is validated, can go to disk
        let spaceAuth = {
            Cache.SpaceAuth.Url = url
            Cache.SpaceAuth.Space = space
            Cache.SpaceAuth.Token = token
        }

        Cache.addSpaceAuth spaceAuth
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

let logout space =
    Cache.removeSpaceAuth space
