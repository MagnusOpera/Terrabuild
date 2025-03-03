module Terrabuild.Tests.SourceControls
open NUnit.Framework
open System.IO

[<Test>]
let ``Load Push``() =
    let event = SourceControls.GitHubEventReader.read "TestFiles/GitHub/push-event.json"
    printfn $"{event}"
    failwith "toto"
    ()

[<Test>]
let ``Load Merge``() =
    ()

[<Test>]
let ``Load Squash``() =
    ()
