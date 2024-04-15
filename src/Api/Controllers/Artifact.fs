namespace Api.Controllers

open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Api
open AspNetExtensions
open Microsoft.AspNetCore.Mvc.ModelBinding


type ArtifactLocationOutput = {
    Type: StoreType
    Url: string
}


[<ApiController; Route("artifact")>]
type ArtifactController (logger : ILogger<AuthController>, appSettings: AppSettings) =
    inherit ControllerBase()

    [<HttpGet; Route("url")>]
    member _.Url([<FromQuery; BindRequired>] path: string, [<FromQuery>] write: bool): ActionResult<ArtifactLocationOutput> =
        let tpe, url =
            match appSettings.Store with
            | Local settings ->
                StoreType.Local, Some $"{base.Request.Scheme}://{base.Request.Host}/store?path={path}"
            | Azure settings ->
                StoreType.Azure, None

        match url with
        | Some url ->
            let response = { Type = tpe; Url = url }
            !> base.Ok(response)
        | _ -> !> base.BadRequest()
