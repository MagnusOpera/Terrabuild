module Storages.Factory

let create (space: string option): Storage =
    let (storage: Storage), reason =
        match space with
        | None -> Local(), " (no space configured)"
        | Some space ->
            match Cache.readAuthToken() with
            | None -> Local(), " (not authenticated)"
            | Some authToken ->
                let accessToken = Auth.createAccessToken space authToken
                AzureBlobStorage accessToken, ""


    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} cache is {storage.Name}{reason}" |> Terminal.writeLine
    storage
