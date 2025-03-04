module Terrabuild.Tests.SourceControls
open NUnit.Framework
open SourceControls.GitHubEventReader
open FsUnit

[<Test>]
let ``Load Push Main``() =
    let event = read "TestFiles/GitHub/push-event.json"
    let expected = {
        After = Some "422506d9f6e2ba9e0f0f0b5a58400d40050ff460"
        Before = Some "00fd263492b760e55a4f4feefb6abf82692d35e4"
        Commits = Some [
            { Author = { Email = "pierre@magnusopera.io"
                         Name = "Pierre Chalamet"}
              Id = "422506d9f6e2ba9e0f0f0b5a58400d40050ff460"
              Message = "push event"
            }
        ]
    }

    event |> should equal expected

[<Test>]
let ``Load Push Branch``() =
    let event = read "TestFiles/GitHub/push-branch-event.json"
    let expected = {
        After = Some "d13520514796c02715becdc28910ac2a50407813"
        Before = Some "0000000000000000000000000000000000000000"
        Commits = Some [
            { Author = { Email = "pierre@magnusopera.io"
                         Name = "Pierre Chalamet"}
              Id = "d13520514796c02715becdc28910ac2a50407813"
              Message = "push in branch"
            }
        ]
    }

    event |> should equal expected


[<Test>]
let ``Load Merge``() =
    let event = read "TestFiles/GitHub/merge-event.json"
    let expected = {
        After = Some "8aeab5f8ceb43063b00e03d835c2e2c57dac9615"
        Before = Some "422506d9f6e2ba9e0f0f0b5a58400d40050ff460"
        Commits = Some [
            { Author = { Email = "pierre@magnusopera.io"
                         Name = "Pierre Chalamet"}
              Id = "d13520514796c02715becdc28910ac2a50407813"
              Message = "push in branch"
            }
            { Author = { Email = "pierre@magnusopera.io"
                         Name = "Pierre Chalamet"}
              Id = "8aeab5f8ceb43063b00e03d835c2e2c57dac9615"
              Message = "Merge pull request #3 from MagnusOpera/feature/merge\n\npush in branch"
            }            
        ]
    }

    event |> should equal expected

[<Test>]
let ``Load Squash``() =
    let event = read "TestFiles/GitHub/squash-event.json"
    let expected = {
        After = Some "053792c34bdcc1747818789f6619f4d7675f79bc"
        Before = Some "8aeab5f8ceb43063b00e03d835c2e2c57dac9615"
        Commits = Some [
            { Author = { Email = "pierre@magnusopera.io"
                         Name = "Pierre Chalamet"}
              Id = "053792c34bdcc1747818789f6619f4d7675f79bc"
              Message = "squash (#4)"
            }
        ]
    }

    event |> should equal expected


[<Test>]
let ``Load Dispatch``() =
    let event = read "TestFiles/GitHub/dispatch-event.json"
    let expected = {
        After = None
        Before = None
        Commits = None
    }

    event |> should equal expected
