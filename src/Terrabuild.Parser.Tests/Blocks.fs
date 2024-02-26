module Terrabuild.Parser.Tests
open System.IO
open NUnit.Framework
open FsUnit
open AST

[<Test>]
let test_file() =
    let content = File.ReadAllText("PROJECT")
    let blocks = FrontEnd.parse content
    for block in blocks do
        printfn $"{block}"
    Assert.IsFalse(true)
