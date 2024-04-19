namespace Storages
open Azure.Storage.Blobs
open Serilog
open FSharp.Data
open System


type AzureArtifactLocationOutput = {
    Uri: string
}

type AzureBlobStorage(accessToken: string) =
    inherit Storage()

    let getBlobClient path =
        let baseUrl = DotNetEnv.Env.GetString("TERRABUILD_API_URL", "https://api.terrabuild.io")
        let url = Uri(Uri(baseUrl), $"/artifact?path={path}").ToString()
        let headers = [
            HttpRequestHeaders.Accept HttpContentTypes.Json
            HttpRequestHeaders.ContentType HttpContentTypes.Json
            HttpRequestHeaders.Authorization $"Bearer {accessToken}" ]
        let response =
            Http.RequestString(url = url, headers = headers, httpMethod = HttpMethod.Get)
            |> FSharpJson.Deserialize<AzureArtifactLocationOutput>
        let uri = Uri(response.Uri)
        let container = BlobContainerClient(uri)
        let blobClient = container.GetBlobClient(path)
        blobClient

    override _.Name = "Azure Blob Storage"

    override _.Exists id =
        let blobClient = getBlobClient id
        try
            let res = blobClient.Exists()
            res.Value
        with
        | :? Azure.RequestFailedException as exn when exn.Status = 404 -> false
        | exn ->
            Log.Fatal(exn, "AzureBlobStorage: failed to download '{Id}'", id)
            reraise()


    override _.TryDownload id =
        let blobClient = getBlobClient id
        let tmpFile = System.IO.Path.GetTempFileName()
        try
            blobClient.DownloadTo(tmpFile) |> ignore
            Log.Debug("AzureBlobStorage: download of '{Id}' successful", id)
            Some tmpFile
        with
        | :? Azure.RequestFailedException as exn when exn.Status = 404 ->
            Log.Fatal("AzureBlobStorage: '{Id}' does not exist", id)
            System.IO.File.Delete(tmpFile)
            None
        | exn ->
            Log.Fatal(exn, "AzureBlobStorage: failed to download '{Id}'", id)
            reraise()


    override _.Upload id summaryFile =
        try
            let blobClient = getBlobClient id
            blobClient.Upload(summaryFile, true) |> ignore
            Log.Debug("AzureBlobStorage: upload of '{Id}' successful", id)
        with
        | exn ->
            Log.Fatal(exn, "AzureBlobStorage: upload of '{Id}' failed", id)
            reraise()
