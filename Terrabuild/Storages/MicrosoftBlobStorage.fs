namespace Storages
open Azure.Storage.Blobs

type MicrosoftBlobStorage() =
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
            container.CreateIfNotExists() |> ignore
        with
            | :? Azure.RequestFailedException -> ()

        container

    override _.TryDownload id =
        let blobClient = container.GetBlobClient(id)
        let tmpFile = System.IO.Path.GetTempFileName()
        try
            blobClient.DownloadTo(tmpFile) |> ignore
            Some tmpFile
        with
            _ ->
                System.IO.File.Delete(tmpFile)
                None

    override _.Upload id summaryFile =
        let blobClient = container.GetBlobClient(id)
        blobClient.Upload(summaryFile, true) |> ignore
