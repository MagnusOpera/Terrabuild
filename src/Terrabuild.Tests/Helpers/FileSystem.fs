module Terrabuild.Tests.FileSystem
open FileSystem
open FsUnit
open NUnit.Framework
open System.IO

[<Test>]
let ``Detect new files``() =
    let before = "TestFiles" |> createSnapshot ["**/*.txt"]
    File.WriteAllText("TestFiles/tutu.txt", "tutu")
    let after = "TestFiles" |> createSnapshot ["**/*.txt"]

    let diff = after - before

    let expected = [ "TestFiles/tutu.txt" |> IO.fullPath ]

    diff
    |> should equal expected
