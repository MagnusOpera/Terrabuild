namespace Contracts
open System


type IApiClient =
    abstract BuildStart: branchOrTag:string -> commit:string -> configuration: string -> note: string option -> tag: string option -> targets:string seq -> force:bool -> retry:bool -> ci:string option -> cimetadata:string option -> string
    abstract BuildComplete: buildId:string -> success:bool -> Unit
    abstract BuildAddArtifact: buildId:string -> project:string -> target:string -> projectHash:string -> hash:string  -> files:string list -> size:int -> success:bool -> Unit
    abstract BuildUseArtifact: buildId:string -> projectHash:string -> hash:string -> Unit
    abstract ArtifactGet: path:string -> Uri

