namespace Storages

type Local() =
    inherit Storage()

    override _.TryDownload id = None

    override _.Upload id summaryFile = ()
