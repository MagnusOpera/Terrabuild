module Terrabuild.Tests.Configuration
open Collections
open FsUnit
open NUnit.Framework
open Microsoft.Extensions.FileSystemGlobbing

[<Test>]
let ``Matcher``() =
    let scanFolder = Configuration.scanFolders "tests/simple" (Set ["**/node_modules"; "**/.nuxt"; "**/.vscode"])
    scanFolder "tests/simple/.vscode" |> should equal false
    scanFolder "tests/simple/node_modules" |> should equal false
    scanFolder "tests/simple/toto/node_modules" |> should equal false
    scanFolder "tests/simple/toto/.out" |> should equal true
    scanFolder "tests/simple/toto/tagada.txt" |> should equal true
    scanFolder "tests/simple/src" |> should equal true
