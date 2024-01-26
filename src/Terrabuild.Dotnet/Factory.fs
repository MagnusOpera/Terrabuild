namespace Terrabuild.Dotnet
open Extensions
open System.ComponentModel.Composition

[<Export("dotnet", typeof<IExtensionFactory>)>]
type Factory() =
    interface IExtensionFactory with
        member _.CreateBuilder ctx =
            Builder(ctx)
