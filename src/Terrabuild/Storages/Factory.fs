module Storages.Factory

let create (space: string option): Storage =
    match space with
    | None -> Local()
    | Some space ->
        match Cache.readAuthToken() with
        | None -> Local()
        | Some authToken ->
            let accessToken = Auth.createAccessToken space authToken
            AzureBlobStorage accessToken
