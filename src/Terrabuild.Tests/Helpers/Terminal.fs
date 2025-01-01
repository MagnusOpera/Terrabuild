namespace Terrabuild.Tests
open NUnit.Framework
open FsUnit
open NeoSmart.Unicode
open System.Linq

module Helpers =
    [<Test>]
    let ``center string 🎉``() =
        let centered = Terminal.center "🎉"
        centered |> should equal "🎉"

    [<Test>]
    let ``center string ✘``() =
        let centered = Terminal.center "✘"
        centered |> should equal "✘ "

    [<Test>]
    let ``center string ⚙️``() =
        let centered = Terminal.center "⚙️"
        centered |> should equal "⚙️"
