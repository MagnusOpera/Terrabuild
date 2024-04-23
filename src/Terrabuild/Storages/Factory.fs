module Storages.Factory

let create (api: Contracts.IApiClient option): Contracts.Storage =
    let (storage: Contracts.Storage) =
        match api with
        | None -> Local()
        | Some api -> AzureBlobStorage(api)

    storage
