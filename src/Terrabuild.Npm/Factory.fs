namespace Terrabuild.Npm
open System.ComponentModel.Composition
open Extensions


[<Export("npm", typeof<IExtensionFactory>)>]
type Factory() =
    interface IExtensionFactory with
        member _.CreateBuilder ctx =
            Builder()
