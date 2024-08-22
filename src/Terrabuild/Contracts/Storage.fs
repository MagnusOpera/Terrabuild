namespace Contracts


type IStorage =
    abstract Exists: projectHash:string -> targetHash:string -> part:string -> bool
    abstract TryDownload: projectHash:string -> targetHash:string -> part:string -> string option
    abstract Upload: projectHash:string -> targetHash:string -> part:string -> summaryFile:string -> unit
    abstract Name: string
