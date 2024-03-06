namespace Storages
open Azure.Storage.Blobs
open Serilog

type AzureBlobStorage() =
    inherit Storage()

    let connString =
        let connString = System.Environment.GetEnvironmentVariable("TERRABUILD_AZURE_BLOB_STORAGE")
        if connString |> isNull then failwith "Please configure TERRABUILD_AZURE_BLOB_STORAGE environment variable"
        connString

    let client = BlobServiceClient(connString)

    let container =
        let container = client.GetBlobContainerClient("buildcache")

        // ensure container is created (in case we need to write to blob storage)
        try
            Log.Debug("AzureBlobStorage: creating Azure Blob Storage container")
            container.CreateIfNotExists() |> ignore
            Log.Debug("AzureBlobStorage: container created")
        with
        | exn ->
            Log.Fatal(exn, "AzureBlobStorage: container creation failed")
            reraise()

        container

    override _.TryDownload id =
        let blobClient = container.GetBlobClient(id)
        let tmpFile = System.IO.Path.GetTempFileName()
        try
            blobClient.DownloadTo(tmpFile) |> ignore
            Log.Debug("AzureBlobStorage: download of '{Id}' successful", id)
            Some tmpFile
        with
        | exn ->
            Log.Fatal(exn, "AzureBlobStorage: download of '{Id}' failed", id)
            System.IO.File.Delete(tmpFile)
            None

    override _.Upload id summaryFile =
        try
            let blobClient = container.GetBlobClient(id)
            blobClient.Upload(summaryFile, true) |> ignore
            Log.Debug("AzureBlobStorage: upload of '{Id}' successful", id)
        with
        | exn ->
            Log.Fatal(exn, "AzureBlobStorage: upload of '{Id}' failed", id)
            reraise()
