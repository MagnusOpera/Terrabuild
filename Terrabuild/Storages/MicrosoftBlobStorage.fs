namespace Storages

type MicrosoftBlobStorage() =
    inherit Storage()

    override _.TryDownload id = None
    override _.Upload id summaryFile = ()
