module Scripting
open System.Reflection
open FSharp.Compiler.CodeAnalysis
open System.IO
open FSharp.Compiler.Diagnostics


let checker = FSharpChecker.Create()
let mutable assemblies = Map.empty

let loadScript (scriptFile: string) =
    let scriptFile = Path.GetFullPath(scriptFile)
    match assemblies |> Map.tryFind scriptFile with
    | Some assembly ->
        assembly
    | _ -> 
        let outputDllName = $"{Path.GetTempFileName()}.dll"

        let compilerArgs = [|
            "-a"; scriptFile
            "--targetprofile:netcore"
            "--target:library"
            $"--out:{outputDllName}"
        |]

        let errors, _ = checker.Compile(compilerArgs) |> Async.RunSynchronously
        let firstError = errors |> Array.tryFind (fun x -> x.Severity = FSharpDiagnosticSeverity.Error)
        if firstError <> None then failwithf $"Error while compiling script {scriptFile}: {firstError.Value}"

        let assembly = Assembly.LoadFile outputDllName
        assemblies <- assemblies |> Map.add scriptFile assembly
        assembly

