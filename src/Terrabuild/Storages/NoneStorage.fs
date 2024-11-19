namespace Storages

type NoneStorage() =
    interface Contracts.IStorage with
        override _.Exists id = false

        override _.TryDownload id = None

        override _.Upload id summaryFile = ()
