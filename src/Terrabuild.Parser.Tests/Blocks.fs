module Terrabuild.Parser.Tests
open System.IO
open NUnit.Framework

[<Test>]
let test_workspace() =
    let content = File.ReadAllText("WORKSPACE")
    let workspace = FrontEnd.parse content
    printfn $"{workspace}"
    Assert.IsFalse(true)
