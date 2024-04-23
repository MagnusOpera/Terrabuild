module Auth
open System.Net



let login token =
    // try exchanging this token with a real token
    // if successful token is validated
    try
        Api.Factory.authenticate token

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

