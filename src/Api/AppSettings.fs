namespace Api

type AuthSettings = {
    Issuer: string
    Secret: string
}

type AppSettings = {
    Auth: AuthSettings
}
