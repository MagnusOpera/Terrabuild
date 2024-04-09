module Terrabuild.Tests.IO
open IO
open FsUnit
open NUnit.Framework

[<Test>]
let ``verify combine path``() =
    let parentPath = "/toto/titi"

    combinePath parentPath "tutu"
    |> should equal "/toto/titi/tutu"

    combinePath parentPath "/tutu"
    |> should equal "/tutu"

[<Test>]
let ``verify path classification``() =
    match "TestFiles" with
    | Directory path -> path |> should equal "TestFiles"
    | _ -> Assert.Inconclusive()

    match "TestFiles/toto.txt" with
    | File path -> path |> should equal "TestFiles/toto.txt"
    | _ -> Assert.Inconclusive()

    match "TestFiles/unknown.txt" with
    | None path -> path |> should equal "TestFiles/unknown.txt"
    | _ -> Assert.Inconclusive()

[<Test>]
let ``verify path absolute/relative``() =
    let workDir = TestContext.CurrentContext.WorkDirectory

    let fullPath = IO.fullPath "TestFiles/toto.txt"
    fullPath |> should equal (combinePath workDir "TestFiles/toto.txt")

    let relativePath = fullPath |> relativePath workDir
    relativePath |> should equal "TestFiles/toto.txt"
