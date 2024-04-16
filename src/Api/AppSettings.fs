namespace Api

[<CLIMutable>]
type AuthSettings = {
    Issuer: string
    Secret: string
}

type LocalSettings = {
    Url: string
}

type AzureSettings = {
    ConnectionString: string
}

type StoreSettings =
    | Local of LocalSettings
    // | Azure of AzureSettings

[<CLIMutable>]
type AppSettings = {
    Auth: AuthSettings
    Store: StoreSettings
}
