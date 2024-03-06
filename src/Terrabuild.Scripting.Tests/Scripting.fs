module Terrabuild.Scripting.Scripting.Tests

open NUnit.Framework
open FsUnit
open Terrabuild.Extensibility
open Terrabuild.Expressions


[<Test>]
let loadScript() =
    let script = Terrabuild.Scripting.loadScript [ "Terrabuild.Extensibility.dll" ] "TestFiles/Toto.fsx"
    let invocable = script.GetMethod("Tagada")
    let context = { InitContext.Directory = "this is a path"; InitContext.CI = false }
    let args = Value.Map (Map [ "context", Value.Object context])
    let res = invocable.Value.Invoke args
    res |> should equal context.Directory

[<Test>]
let loadScriptWithError() =
    (fun () -> Terrabuild.Scripting.loadScript [ "Terrabuild.Extensibility.dll" ] "TestFiles/Failure.fsx" |> ignore)
    |> should (throwWithMessage "Failed to identify function scope (either module or root class 'Failure')") typeof<System.Exception>
