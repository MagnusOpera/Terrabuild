namespace Terrabuild.Dotnet
open Terrabuild.Extensibility
open System.ComponentModel.Composition

[<Export("dotnet", typeof<IExtensionFactory>)>]
type Factory() =
    interface IExtensionFactory with
        member _.CreateBuilder ctx =
            Builder(ctx)
