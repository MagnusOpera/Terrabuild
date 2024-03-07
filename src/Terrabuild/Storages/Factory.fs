module Storages.Factory

let create(): Storage =
    if AzureBlobStorage.Detect() then
        AzureBlobStorage()
    else
        Local()
