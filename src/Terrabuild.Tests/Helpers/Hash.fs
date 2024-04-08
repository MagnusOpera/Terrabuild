module Tests.Hash
open Hash
open FsUnit
open NUnit.Framework

[<Test>]
let ``verify sha256``() =
    "toto\ntiti"
    |> sha256
    |> String.toLower
    |> should equal "539682fb0b380dbbf3d7085a22055092ba6291638a576eea139cf4b400377015"

[<Test>]
let ``verify sha256 list``() =
    [ "toto"; "titi" ]
    |> sha256strings
    |> String.toLower
    |> should equal "539682fb0b380dbbf3d7085a22055092ba6291638a576eea139cf4b400377015"

[<Test>]
let ``verify sha256 files``() =
    [ "TestFiles/toto.txt"; "TestFiles/titi.txt" ]
    |> sha256files
    |> String.toLower
    |> should equal "ce8a6882216d40d8a9245067c51fd54dd40e9363001b4b9658197a71eb2250cb"
