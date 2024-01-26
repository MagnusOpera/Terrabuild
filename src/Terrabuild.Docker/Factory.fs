namespace Terrabuild.Docker
open Extensions
open System.ComponentModel.Composition


[<Export("docker", typeof<IExtensionFactory>)>]
type Factory() =
    interface IExtensionFactory with
        member _.CreateBuilder ctx =
            Builder(ctx)
