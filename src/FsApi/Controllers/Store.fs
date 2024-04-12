namespace Api.Controllers

open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Api
open Microsoft.AspNetCore.Authorization
open AspNetExtensions
open Microsoft.AspNetCore.Http
open System.Threading.Tasks


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

    [<HttpGet; Route("exists")>]
    member _.Exists([<FromQuery>] path: string): ActionResult =
        printfn $"Exists {path}"

        match appSettings.Store.Type with
        | StoreType.Local -> if LocalStore.exists appSettings.Store.Uri path then base.Ok() else base.NotFound()
        | _ -> failwith $"Unknown store type {appSettings.Store.Type}"

    [<HttpGet; Route("read/{path}")>]
    member _.Read([<FromQuery>] path: string): Task<byte array> =
        match appSettings.Store.Type with
        | StoreType.Local -> LocalStore.read appSettings.Store.Uri path |> Task.FromResult
        | _ -> failwith $"Unknown store type {appSettings.Store.Type}"

    [<HttpPut; Route("write/{path}")>]
    member _.Write(path: string): ActionResult =
        match appSettings.Store.Type with
        | StoreType.Local -> if LocalStore.write appSettings.Store.Uri path then base.Ok() else base.NotFound()
        | _ -> failwith $"Unknown store type {appSettings.Store.Type}"
