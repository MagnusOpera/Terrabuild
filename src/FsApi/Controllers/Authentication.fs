namespace Api.Controllers

open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Api
open Microsoft.AspNetCore.Authorization
open AspNetExtensions

type CredentialsInput = {
    Organization: string
    Email: string
    Password: string
}

type TokenInput = {
    Token: string
}

type LoginInput =
    | Credentials of CredentialsInput
    | Token of TokenInput


type LoginOutput = {
    AuthToken: string
    RefreshToken: string
}



[<ApiController>]
[<Route("auth")>]
type AuthController (logger : ILogger<AuthController>, appSettings: AppSettings) =
    inherit ControllerBase()


    [<HttpPost("login"); AllowAnonymous>]
    member _.Login(input: LoginInput): ActionResult<LoginOutput> =
        printfn $"{input}"
        match input with
        | Credentials credentials ->
            let token = JWT.createToken credentials.Organization credentials.Email appSettings.Auth.Secret
            let response = { AuthToken = token; RefreshToken = "troulala" }
            !> response
        | _ ->
            !> base.Unauthorized()

    [<HttpGet; Route("logout")>]
    member _.Logout() =
        ()
