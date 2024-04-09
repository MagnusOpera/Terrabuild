module Terrabuild.Tests.Collections
open Collections
open FsUnit
open NUnit.Framework

[<Test>]
let ``convert dictionary to map``() =
    let dict = System.Collections.Generic.Dictionary<string, int>()
    dict.Add("toto", 1)
    dict.Add("titi", 2)
    dict.Add("tutu", 3)

    let expected = Map [ "toto", 1
                         "titi", 2
                         "tutu", 3 ]

    dict
    |> Map.ofDict
    |> should equal expected

[<Test>]
let ``ignore None from Set map``() =
    let s = Set [ 0..10 ]

    let expected = Set [ 0; 2; 4; 6; 8; 10 ]

    s
    |> Set.choose (fun x -> 
        match x % 2 with
        | 0 -> Some x
        | _ -> None)
    |> should equal expected

[<Test>]
let ``collect Set``() =
    let s = Set [ 0..10 ]

    let expected = Set [ 0..20 ]
    
    s
    |> Set.collect (fun x -> Set [ 0..x*2 ])
    |> should equal expected
