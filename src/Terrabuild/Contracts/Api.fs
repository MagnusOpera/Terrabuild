namespace Contracts
open System


type IApiClient =
    abstract StartBuild: branchOrTag:string -> commit:string -> configuration:string -> note:string option -> tag:string option -> targets:string seq -> force:bool -> retry:bool -> ci: bool -> ciname:string option -> cimetadata:string option -> string
    abstract CompleteBuild: buildId:string -> success:bool -> Unit

    abstract CreateArtifact: buildId:string -> project:string -> target:string -> projectHash:string -> targetHash:string -> Unit
    abstract CompleteArtifact: buildId:string -> projectHash:string -> targetHash:string -> parts:string list -> success:bool -> Unit

    abstract UseArtifact: buildId:string -> projectHash:string -> targetHash:string -> Unit

    abstract GetArtifactPart: projectHash:string -> targetHash:string -> part:string -> Uri
