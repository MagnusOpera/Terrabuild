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

let systemExtensions = Terrabuild.Extensions.Factory.systemScripts |> Map.map (fun _ _ -> Extension.Empty)

// NOTE: when app in package as a single file, Terrabuild.Assembly can't be found... So instead of providing 
//       Terrabuild.Extensibility assembly, the Terrabuild main assembly is provided instead
//       ¯\_(ツ)_/¯
let terrabuildDir = Diagnostics.Process.GetCurrentProcess().MainModule.FileName |> FS.parentDirectory
let terrabuildExtensibility =
    let path = FS.combinePath terrabuildDir "Terrabuild.Extensibility.dll"
    if File.Exists(path) then path
    else Reflection.Assembly.GetExecutingAssembly().Location

let lazyLoadScript (name: string) (script: string option) =
    let initScript () =
        match script with
        | Some script -> loadScript [ terrabuildExtensibility ] script
        | _ ->
            match Terrabuild.Extensions.Factory.systemScripts |> Map.tryFind name with
            | Some sysTpe -> Script(sysTpe)
            | _ -> TerrabuildException.Raise($"Script is not defined for extension '{name}'")

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
                | method when method.StartsWith("__") -> TargetNotFound
                | _ -> invokeScriptMethod "__dispatch__"

        invokeScriptMethod method
