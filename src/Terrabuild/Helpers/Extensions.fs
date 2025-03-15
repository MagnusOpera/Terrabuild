module Extensions
open System
open System.IO
open Terrabuild.Scripting
open Terrabuild.Expressions
open Terrabuild.Configuration.AST
open Errors

type InvocationResult<'t> =
    | Success of 't
    | ScriptNotFound
    | TargetNotFound
    | ErrorTarget of Exception

let systemExtensions =
    Terrabuild.Extensions.Factory.systemScripts
    |> Seq.map (fun kvp -> Extension.Build kvp.Key [])
    |> Map.ofSeq

// NOTE: when app in package as a single file, Terrabuild.Assembly can't be found...
//       this means native deployments are not supported ¯\_(ツ)_/¯
let terrabuildDir = Diagnostics.Process.GetCurrentProcess().MainModule.FileName |> FS.parentDirectory
let terrabuildExtensibility =
    let path = FS.combinePath terrabuildDir "Terrabuild.Extensibility.dll"
    path

let lazyLoadScript (name: string) (script: string option) =
    let initScript () =
        match script with
        | Some script ->
            loadScript [ terrabuildExtensibility ] script
        | _ ->
            match Terrabuild.Extensions.Factory.systemScripts |> Map.tryFind name with
            | Some sysTpe -> Script(sysTpe)
            | _ -> Errors.raiseSymbolError $"Script is not defined for extension '{name}'"

    lazy(initScript())

let getScript (extension: string) (scripts: Map<string, Lazy<Script>>) =
    scripts
    |> Map.tryFind extension
    |> Option.map (fun script -> script.Value)

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
                | :? Reflection.TargetInvocationException as exn -> ErrorTarget exn.InnerException
                | exn -> ErrorTarget exn
            | None ->
                match method with
                | method when method.StartsWith("__") -> TargetNotFound
                | _ -> invokeScriptMethod "__dispatch__"

        invokeScriptMethod method
