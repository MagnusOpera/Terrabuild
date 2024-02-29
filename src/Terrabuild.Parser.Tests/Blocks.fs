module Terrabuild.Parser.Tests
open System.IO
open NUnit.Framework

[<Test>]
let test_workspace() =
    let content = File.ReadAllText("WORKSPACE")
    let workspace = FrontEnd.parseWorkspace content
    printfn $"{workspace}"

[<Test>]
let test_project() =
    let content = File.ReadAllText("PROJECT")
    let build = FrontEnd.parseProject content
    printfn $"{build}"
    