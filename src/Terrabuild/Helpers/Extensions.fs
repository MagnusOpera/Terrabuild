module Extensions
open System
open System.IO
open Terrabuild.Scripting
open Terrabuild.Expressions
open Terrabuild.Configuration.AST

type InvocationResult<'t> =
    | Success of 't
    | ScriptNotFound
    | TargetNotFound
    | ErrorTarget of Exception

// well-know provided extensions
// do not forget to add reference when adding new implementation
let systemScripts =
    Map [
        "@docker", typeof<Terrabuild.Extensions.Docker>
        "@dotnet", typeof<Terrabuild.Extensions.Dotnet>
        "@make", typeof<Terrabuild.Extensions.Make>
        "@npm", typeof<Terrabuild.Extensions.Npm>
        "@null", typeof<Terrabuild.Extensions.Null>
        "@shell", typeof<Terrabuild.Extensions.Shell>
        "@terraform", typeof<Terrabuild.Extensions.Terraform>
    ]

let systemExtensions =
    systemScripts |> Map.map (fun _ _ -> Extension.Empty)


let loadStorage name : Storages.Storage =
    match name with
    | None -> Storages.Local()
    | Some "azureblob" -> Storages.AzureBlobStorage()
    | _ -> failwith $"Unknown storage '{name}'"

let loadSourceControl name: SourceControls.SourceControl =
    match name with
    | None -> SourceControls.Local()
    | Some "github" -> SourceControls.GitHub()
    | _ -> failwith $"Unknown source control '{name}'"

// NOTE: when app in package as a single file, this break - so instead of providing 
//       Terrabuild.Extensibility assembly, the Terrabuild main assembly is provided
//       ¯\_(ツ)_/¯
let terrabuildDir = Diagnostics.Process.GetCurrentProcess().MainModule.FileName |> IO.parentDirectory
let terrabuildExtensibility =
    let path = IO.combinePath terrabuildDir "Terrabuild.Extensibility.dll"
    if File.Exists(path) then path
    else Reflection.Assembly.GetExecutingAssembly().Location

let lazyLoadScript (name: string) (ext: Extension) =
    let initScript () =
        match ext.Script with
        | Some script -> loadScript [ terrabuildExtensibility ] script
        | _ ->
            match systemScripts |> Map.tryFind name with
            | Some sysTpe -> Script(sysTpe)
            | _ -> failwith $"Script is not defined for extension '{name}'"

    lazy(initScript())

let getScript (extension: string) (scripts: Map<string, Lazy<Script>>) =
    match scripts |> Map.tryFind extension with
    | None -> None
    | Some script -> script.Value |> Some

let invokeScriptMethod<'r> (method: string) (args: Value) (script: Script option) =
    match script with
    | None -> ScriptNotFound
    | Some script ->
        let rec invokeScriptMethod (method: string) =
            let invocable = script.GetMethod(method)
            match invocable with
            | Some invocable ->
                try
                    Success (invocable.Invoke<'r> args)
                with
                | exn -> ErrorTarget exn
            | None ->
                match method with
                | "__init__"
                | "__dispatch__"
                | "__optimize__" -> TargetNotFound
                | _ -> invokeScriptMethod "__dispatch__"

        invokeScriptMethod method
