namespace Storages

[<AbstractClass>]
type Storage() =
    abstract TryDownload: id:string -> string option
    abstract Upload: id:string -> summaryFile:string -> unit
    abstract Name: string
