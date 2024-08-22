namespace Storages

type Local() =
    interface Contracts.IStorage with
        override _.Name = "Local"

        override _.Exists projectHash targetHash part = false

        override _.TryDownload projectHash targetHash part = None

        override _.Upload projectHash targetHash part summaryFile = ()
