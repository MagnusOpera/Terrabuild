namespace Terrabuild.Make
open Extensions
open System.ComponentModel.Composition

[<Export("make", typeof<IExtensionFactory>)>]
type MakeFactory() =
    interface IExtensionFactory with
        member _.CreateBuilder ctx =
            Builder()
