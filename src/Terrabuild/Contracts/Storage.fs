namespace Contracts


type IStorage =
    abstract Exists: id:string -> bool
    abstract TryDownload: id:string -> string option
    abstract Upload: id:string -> summaryFile:string -> unit
    abstract Name: string
