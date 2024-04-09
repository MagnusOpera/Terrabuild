module Terrabuild.Tests.FS
open FS
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
    | FS.Directory path -> path |> should equal "TestFiles"
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

    let fullPath = FS.fullPath "TestFiles/toto.txt"
    fullPath |> should equal (combinePath workDir "TestFiles/toto.txt")

    let relativePath = fullPath |> relativePath workDir
    relativePath |> should equal "TestFiles/toto.txt"

[<Test>]
let ``verify relative workspace conversion``() =
    let workspaceDir = "/home/toto/src"
    let currentDir = "/home/toto/src/project"
    
    workspaceRelative workspaceDir currentDir "tagada"
    |> should equal "project/tagada"

    workspaceRelative workspaceDir currentDir "../project/tagada"
    |> should equal "project/tagada"

[<Test>]
let ``verify absolute workspace conversion``() =
    let workspaceDir = "/home/toto/src"
    let currentDir = "/home/toto/src/project"
    
    workspaceRelative workspaceDir currentDir "/tagada"
    |> should equal "tagada"
