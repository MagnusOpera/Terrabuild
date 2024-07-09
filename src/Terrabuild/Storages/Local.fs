namespace Storages

type Local() =
    interface Contracts.IStorage with
        override _.Name = "Local"

        override _.Exists id = false

        override _.TryDownload id = None

        override _.Upload id summaryFile = ()
