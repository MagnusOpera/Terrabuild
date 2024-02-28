module Terrabuild.Parser.Tests
open System.IO
open NUnit.Framework

// [<Test>]
// let test_workspace() =
//     let content = File.ReadAllText("WORKSPACE")
//     let workspace = WorkspaceFrontEnd.parse content
//     printfn $"{workspace}"
//     Assert.IsFalse(true)

[<Test>]
let test_project() =
    let content = File.ReadAllText("BUILD")
    let build = BuildFrontEnd.parse content
    printfn $"{build}"
    Assert.IsFalse(true)
