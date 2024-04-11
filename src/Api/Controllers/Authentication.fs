namespace Api.Controllers

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Api
open Microsoft.AspNetCore.Authorization


type LoginInput = {
    Organization: string
    Email: string
    Password: string
}

[<ApiController>]
[<Route("[controller]")>]
type AuthController (logger : ILogger<AuthController>) =
    inherit ControllerBase()

    [<HttpPost; AllowAnonymous>]
    member _.Login(input: LoginInput) =
        printfn $"{input}"

    [<HttpGet>]
    member _.Logout() =
        ()
