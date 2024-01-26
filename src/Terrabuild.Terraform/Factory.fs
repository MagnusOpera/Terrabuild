namespace Terrabuild.Terraform
open System.ComponentModel.Composition
open Terrabuild.Extensibility



[<Export("terraform", typeof<IExtensionFactory>)>]
type TerraformFactory() =
    interface IExtensionFactory with
        member _.CreateBuilder ctx =
            Builder()
