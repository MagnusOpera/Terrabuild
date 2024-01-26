namespace Terrabuild.Terraform
open System.ComponentModel.Composition
open Extensions



[<Export("terraform", typeof<IExtensionFactory>)>]
type TerraformFactory() =
    interface IExtensionFactory with
        member _.CreateBuilder ctx =
            Builder()
