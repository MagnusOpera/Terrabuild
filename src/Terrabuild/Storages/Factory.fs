module Storages.Factory

let create api: Contracts.IStorage =
    match api with
    | None -> NoneStorage()
    | Some api -> InsightsBlobStorage(api)
