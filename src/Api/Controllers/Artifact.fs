namespace Api.Controllers

open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Api
open AspNetExtensions
open Microsoft.AspNetCore.Mvc.ModelBinding
open Azure.Storage.Blobs
open Azure.Storage.Sas
open System
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Authentication.JwtBearer

[<RequireQualifiedAccess>]
type ArtifactLocationOutput =
    | Local of url:string
    | Azure of token:Uri


[<ApiController; Route("artifact")>]
[<Authorize>]
type ArtifactController (logger : ILogger<AuthController>, appSettings: AppSettings) =
    inherit ControllerBase()

    [<HttpGet>]
    member _.Url([<FromQuery; BindRequired>] path: string, [<FromQuery>] write: bool): ActionResult<ArtifactLocationOutput> =
        let auth = base.HttpContext.GetTokenAsync("access_token") |> Async.AwaitTask |> Async.RunSynchronously
        let token = JWT.getToken auth
        match token |> JWT.getOrganization with
        | Some _ ->
            let location =
                match appSettings.Store with
                | Local _ ->
                    ArtifactLocationOutput.Local $"{base.Request.Scheme}://{base.Request.Host}/store?path={path}"
                // | Azure settings ->
                //     let client = BlobServiceClient(settings.ConnectionString)
                //     let containerClient = client.GetBlobContainerClient(claim.Value)
                //     containerClient.CreateIfNotExists() |> ignore
                //     let blobClient = containerClient.GetBlobClient(path)

                //     let startsOn = DateTime.UtcNow
                //     let expiresOn = startsOn.AddMinutes(2)
                //     let sasBuilder = BlobSasBuilder(StartsOn = startsOn, ExpiresOn = expiresOn)
                //     let permissions = if write then BlobSasPermissions.Write else BlobSasPermissions.Read
                //     sasBuilder.SetPermissions(permissions)
                //     let token = blobClient.GenerateSasUri(sasBuilder)
                //     ArtifactLocationOutput.Azure token
            !> location
        | _ ->
            !> base.Unauthorized()
