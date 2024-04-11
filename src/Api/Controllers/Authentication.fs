namespace Api.Controllers

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Api


type LoginInput = {
    Organization: string
    Email: string
    Password: string
}

[<ApiController>]
[<Route("[controller]")>]
type AuthController (logger : ILogger<AuthController>) =
    inherit ControllerBase()

    [<HttpPost>]
    member _.Login(input: LoginInput) =
        printfn $"{input}"
