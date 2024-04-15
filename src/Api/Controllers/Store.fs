namespace Api.Controllers

open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Api
open Microsoft.AspNetCore.Authorization
open AspNetExtensions
open Microsoft.AspNetCore.Http
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc.ModelBinding


module LocalStore =
    let private ensureStoreExists uri =
        let storePath = uri |> FS.fullPath
        match uri with
        | FS.File _ -> failwith "Found file {uri} instead of directory"
        | FS.Directory _ -> ()
        | _ -> uri |> IO.createDirectory
        storePath

    let exists uri path =
        let storePath = ensureStoreExists uri
        let artifactPath = FS.combinePath storePath path |> FS.fullPath
        printfn $"Local: exists {artifactPath}"
    
        // security note: we must ensure path within store path
        if artifactPath |> String.startsWith storePath |> not then false
        else
            match artifactPath with
            | FS.File _ -> true
            | _ -> false

    let read uri path =
        let storePath = ensureStoreExists uri
        Array.empty<byte>

    let write uri path =
        let storePath = ensureStoreExists uri
        false

[<ApiController; Route("store")>]
type StoreController (logger : ILogger<AuthController>, appSettings: AppSettings) =
    inherit ControllerBase()

    let getArtifactFullPath path storePath =
        let artifactPath = FS.combinePath storePath path |> FS.fullPath
        // security note: we must ensure path within store path
        if artifactPath |> String.startsWith storePath then Some artifactPath
        else None

    let getStoreFullPath() =
        match appSettings.Store with
        | Local settings -> 
            let storePath = settings.Url |> FS.fullPath
            match storePath with
            | FS.File _ -> failwith "Found file {storePath} instead of directory"
            | FS.Directory _ -> ()
            | _ -> storePath |> IO.createDirectory
            storePath
        | _ -> failwith "LocalStore is disabled"

    [<HttpHead>]
    member _.Exists([<FromQuery; BindRequired>] path: string): ActionResult =
        let storePath = getStoreFullPath ()
        match getArtifactFullPath path storePath with
        | Some artifactFile ->
            if IO.exists artifactFile then base.Ok()
            else base.NotFound()
        | _ -> base.NotFound()

    [<HttpGet>]
    member _.Read([<FromQuery; BindRequired>] path: string): ActionResult =
        let storePath = getStoreFullPath()
        match getArtifactFullPath path storePath with
        | Some artifactFile ->
            if IO.exists artifactFile then base.PhysicalFile(artifactFile, "application/octet-stream")
            else base.BadRequest()
        | _ -> base.BadRequest()

    [<HttpPut>]
    member _.Write([<FromQuery; BindRequired>] path: string, [<BindRequired>] content: IFormFile): ActionResult =
        let storePath = getStoreFullPath()
        match getArtifactFullPath path storePath with
        | Some artifactFile ->
            use targetFile = System.IO.File.OpenWrite(artifactFile)
            content.CopyTo(targetFile)
            base.Ok()
        | _ -> base.BadRequest()
