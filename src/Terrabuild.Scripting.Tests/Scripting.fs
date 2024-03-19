module Terrabuild.Scripting.Scripting.Tests

open NUnit.Framework
open FsUnit
open Terrabuild.Extensibility
open Terrabuild.Expressions


[<Test>]
let loadScript() =
    let script = Terrabuild.Scripting.loadScript [ "Terrabuild.Extensibility.dll" ] "TestFiles/Toto.fsx"
    let invocable = script.GetMethod("Tagada")
    let context = { InitContext.Debug= false; InitContext.Directory = "this is a path"; InitContext.CI = false }
    let args = Value.Map (Map [ "context", Value.Object context])
    let res = invocable.Value.Invoke args
    res |> should equal context.Directory

[<Test>]
let loadScriptWithError() =
    (fun () -> Terrabuild.Scripting.loadScript [ "Terrabuild.Extensibility.dll" ] "TestFiles/Failure.fsx" |> ignore)
    |> should (throwWithMessage "Failed to identify function scope (either module or root class 'Failure')") typeof<System.Exception>

[<Test>]
let loadVSSolution() =
    let testDir = System.IO.Path.Combine(NUnit.Framework.TestContext.CurrentContext.TestDirectory, "TestFiles")
    let script = Terrabuild.Scripting.loadScript [ "Terrabuild.Extensibility.dll" ] "TestFiles/VSSolution.fsx"
    let invocable = script.GetMethod("__init__")
    let context = { InitContext.Debug= false; InitContext.Directory = testDir; InitContext.CI = false }
    let args = Value.Map (Map [ "context", Value.Object context])

    let res = invocable.Value.Invoke<Terrabuild.Extensibility.ProjectInfo> args

    let expectedDependencies = Set [ "src"; "src\Terrabuild\Terrabuild.fsproj"; "src\Terrabuild.Configuration\Terrabuild.Configuration.fsproj" ]
    res.Dependencies |> should equal expectedDependencies
