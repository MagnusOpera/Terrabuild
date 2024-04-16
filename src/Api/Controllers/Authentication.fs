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

type LoginOutput = {
    AuthToken: string
    RefreshToken: string
}



[<ApiController>]
[<Route("auth")>]
[<Authorize>]
type AuthController (logger : ILogger<AuthController>, appSettings: AppSettings) =
    inherit ControllerBase()

    [<HttpPost; AllowAnonymous>]
    member _.Login(input: CredentialsInput): ActionResult<LoginOutput> =
        match appSettings.Store with
        | Local _ ->
            let token = JWT.createToken input.Organization input.Email appSettings.Auth.Secret 60 appSettings.Auth
            let refreshToken = JWT.createToken input.Organization input.Email appSettings.Auth.Secret 60 appSettings.Auth
            !> { AuthToken = token; RefreshToken = refreshToken }
        // | Azure _ ->
        //     let token = JWT.createToken input.Organization input.Email appSettings.Auth.Secret 60 appSettings.Auth
        //     let refreshToken = JWT.createToken input.Organization input.Email appSettings.Auth.Secret 60 appSettings.Auth
        //     !> { AuthToken = token; RefreshToken = refreshToken }

    [<HttpPatch; AllowAnonymous>]
    member _.Refresh(input: TokenInput): ActionResult<LoginOutput> =
        printfn $"{input}"
        !> base.BadRequest()
        // let token = JWT.createToken input.Organization input.Email appSettings.Auth.Secret
        // let response = { AuthToken = token; RefreshToken = "troulala" }
        // !> response

    [<HttpDelete>]
    member _.Logout(): ActionResult =
        base.Ok()
