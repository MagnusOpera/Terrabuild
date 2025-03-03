module Terrabuild.Tests.SourceControls
open NUnit.Framework
open SourceControls.GitHubEventReader
open FsUnit

[<Test>]
let ``Load Push Main``() =
    let event = read "TestFiles/GitHub/push-event.json"
    let expected = {
        After = "422506d9f6e2ba9e0f0f0b5a58400d40050ff460"
        Before = "00fd263492b760e55a4f4feefb6abf82692d35e4"
        Commits = [
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
        After = "d13520514796c02715becdc28910ac2a50407813"
        Before = "0000000000000000000000000000000000000000"
        Commits = [
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
        After = "8aeab5f8ceb43063b00e03d835c2e2c57dac9615"
        Before = "422506d9f6e2ba9e0f0f0b5a58400d40050ff460"
        Commits = [
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
        After = "053792c34bdcc1747818789f6619f4d7675f79bc"
        Before = "8aeab5f8ceb43063b00e03d835c2e2c57dac9615"
        Commits = [
            { Author = { Email = "pierre@magnusopera.io"
                         Name = "Pierre Chalamet"}
              Id = "053792c34bdcc1747818789f6619f4d7675f79bc"
              Message = "squash (#4)"
            }
        ]
    }

    event |> should equal expected





[<Test>]
let ``Parent Commits Push Event``() =
    let commits = findParentCommits "TestFiles/GitHub/push-event.json"
    let expected = Set [ "422506d9f6e2ba9e0f0f0b5a58400d40050ff460"
                         "00fd263492b760e55a4f4feefb6abf82692d35e4" ]
    commits |> should equal expected


[<Test>]
let ``Parent Commits Push Branch Event``() =
    let commits = findParentCommits "TestFiles/GitHub/push-branch-event.json"
    let expected = Set [ "d13520514796c02715becdc28910ac2a50407813" ]
    commits |> should equal expected


[<Test>]
let ``Parent Commits Merge Event``() =
    let commits = findParentCommits "TestFiles/GitHub/merge-event.json"
    let expected = Set [ "8aeab5f8ceb43063b00e03d835c2e2c57dac9615"
                         "422506d9f6e2ba9e0f0f0b5a58400d40050ff460"
                         "d13520514796c02715becdc28910ac2a50407813" ]
    commits |> should equal expected

[<Test>]
let ``Parent Commits Squash Event``() =
    let commits = findParentCommits "TestFiles/GitHub/squash-event.json"
    let expected = Set [ "053792c34bdcc1747818789f6619f4d7675f79bc"
                         "8aeab5f8ceb43063b00e03d835c2e2c57dac9615" ]
    commits |> should equal expected
