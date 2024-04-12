namespace Api

[<CLIMutable>]
type AuthSettings = {
    Issuer: string
    Secret: string
}

[<RequireQualifiedAccess>]
type StoreType =
    | Local = 0

[<CLIMutable>]
type StoreSettings = {
    Type: StoreType
    Uri: string
}

[<CLIMutable>]
type AppSettings = {
    Auth: AuthSettings
    Store: StoreSettings
}
