module Storages.Factory

let create (api: ApiClient.IClient option): Storage =
    let (storage: Storage) =
        match api with
        | None -> Local()
        | Some api -> AzureBlobStorage(api)

    storage
