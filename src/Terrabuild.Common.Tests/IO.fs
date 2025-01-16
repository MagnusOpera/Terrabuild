module Terrabuild.Tests.IO
open FsUnit
open NUnit.Framework
open System.IO
open Microsoft.Extensions.FileSystemGlobbing

[<Test>]
let ``Detect new files``() =
    let before = "TestFiles" |> IO.createSnapshot ["**/*.txt"]
    File.WriteAllText("TestFiles/tutu.txt", "tutu")
    let after = "TestFiles" |> IO.createSnapshot ["**/*.txt"]

    let diff = after - before

    let expected = [ "TestFiles/tutu.txt" |> FS.fullPath ]

    diff
    |> should equal expected


[<Test>]
let ``Matcher``() =
    let matcher = Matcher()
    matcher.AddInclude("**/*").AddExcludePatterns(["**/node_modules"; "**/.nuxt"; "**/.vscode"])

    matcher.Match(".vscode").HasMatches |> should equal false
    matcher.Match("node_modules").HasMatches |> should equal false
    matcher.Match("toto/node_modules").HasMatches |> should equal false
    matcher.Match("toto/tagada.txt").HasMatches |> should equal true
