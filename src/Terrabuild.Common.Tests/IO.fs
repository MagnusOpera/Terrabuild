module Terrabuild.Tests.IO
open FsUnit
open NUnit.Framework
open System.IO

[<Test>]
let ``Detect new files``() =
    let before = "TestFiles" |> IO.createSnapshot ["**/*.txt"]
    File.WriteAllText("TestFiles/tutu.txt", "tutu")
    let after = "TestFiles" |> IO.createSnapshot ["**/*.txt"]

    let diff = after - before

    let expected = [ "TestFiles/tutu.txt" |> FS.fullPath ]

    diff
    |> should equal expected
