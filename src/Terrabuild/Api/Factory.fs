module Api.Factory
open System.Net


let authenticate = Auth.authenticate


let create space token =
    match token, space with
    | Some token , Some space -> 
        try
            let api: Contracts.IApiClient = Client(space, token)
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

