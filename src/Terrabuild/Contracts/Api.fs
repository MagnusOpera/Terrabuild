namespace Contracts
open System


type IApiClient =
    abstract BuildStart: branchOrTag:string -> commit:string -> configuration: string -> environment: string -> targets:string seq -> force:bool -> retry:bool -> ci:bool -> string
    abstract BuildComplete: buildId:string -> success:bool -> Unit
    abstract BuildAddArtifact: buildId:string -> project:string -> target:string -> projectHash:string -> hash:string  -> files:string list -> size:int -> success:bool -> Unit
    abstract ArtifactGet: path:string -> Uri

