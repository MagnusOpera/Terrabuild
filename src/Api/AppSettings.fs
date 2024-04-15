namespace Api

[<CLIMutable>]
type AuthSettings = {
    Issuer: string
    Secret: string
}

[<RequireQualifiedAccess>]
type StoreType =
    | Local
    | Azure

type LocalSettings = {
    Url: string
}

type AzureSettings = {
    Account: string
    Key: string
}

type StoreSettings =
    | Local of LocalSettings
    | Azure of AzureSettings

[<CLIMutable>]
type AppSettings = {
    Auth: AuthSettings
    Store: StoreSettings
}
