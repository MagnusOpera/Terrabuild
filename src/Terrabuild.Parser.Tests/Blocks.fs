module Terrabuild.Parser.Tests
open System.IO
open NUnit.Framework

[<Test>]
let test_workspace() =
    let content = File.ReadAllText("WORKSPACE")
    let workspace = FrontEnd.parseWorkspace content
    printfn $"{workspace}"

[<Test>]
let test_build() =
    let content = File.ReadAllText("BUILD")
    let build = FrontEnd.parseBuild content
    printfn $"{build}"
