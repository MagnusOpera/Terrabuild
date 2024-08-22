namespace Storages
open Azure.Storage.Blobs
open Serilog


type AzureArtifactLocationOutput = {
    Uri: string
}

type AzureBlobStorage(api: Contracts.IApiClient) =
    let getBlobClient projectHash targetHash part  =
        let uri = api.GetArtifactPart projectHash targetHash part 
        let container = BlobContainerClient(uri)

        let path = $"{projectHash}/{targetHash}/{part}"
        let blobClient = container.GetBlobClient(path)
        blobClient

    interface Contracts.IStorage with
        override _.Name = "Azure Blob Storage"

        override _.Exists projectHash targetHash part =
            let blobClient = getBlobClient projectHash targetHash part
            try
                let res = blobClient.Exists()
                res.Value
            with
            | :? Azure.RequestFailedException as exn when exn.Status = 404 -> false
            | exn ->
                Log.Fatal(exn, "AzureBlobStorage: failed to download '{Id}'", id)
                reraise()


        override _.TryDownload projectHash targetHash part =
            let blobClient = getBlobClient projectHash targetHash part 
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


        override _.Upload projectHash targetHash part summaryFile =
            try
                let blobClient = getBlobClient projectHash targetHash part
                blobClient.Upload(summaryFile, true) |> ignore
                Log.Debug("AzureBlobStorage: upload of '{Id}' successful", id)
            with
            | exn ->
                Log.Fatal(exn, "AzureBlobStorage: upload of '{Id}' failed", id)
                reraise()
