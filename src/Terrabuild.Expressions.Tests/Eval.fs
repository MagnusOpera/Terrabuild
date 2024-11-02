module Terrabuild.Expressions.Tests

open NUnit.Framework
open FsUnit
open Eval
open Errors

let private evaluationContext = {
    Eval.EvaluationContext.WorkspaceDir = TestContext.CurrentContext.WorkDirectory
    Eval.EvaluationContext.ProjectDir = TestContext.CurrentContext.TestDirectory
    Eval.EvaluationContext.Variables = Map.empty
    Eval.EvaluationContext.Versions = Map.empty
}

[<Test>]
let valueNothing() =
    let expected = Value.Nothing
    let varUsed, result = eval evaluationContext (Expr.Nothing)
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let valueString() =
    let expected = Value.String "toto"
    let varUsed, result = eval evaluationContext (Expr.String "toto")
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let valueBool() =
    let expected = Value.Bool true
    let varUsed, result = eval evaluationContext (Expr.Bool true)
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let valueMap() =
    let expected = Value.Map (Map ["hello", Value.String "world"])
    let varUsed, result = eval evaluationContext (Expr.Map (Map ["hello", Expr.String "world"]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let valueVariable() =
    let expectedUsedVars = Set [ "toto" ]
    let expected = Value.String "titi"
    let context = { evaluationContext with Variables = Map ["toto", Expr.String "titi"] }
    let varUsed, result = eval context (Expr.Variable "toto")
    varUsed |> should equal expectedUsedVars
    result |> should equal expected

[<Test>]
let valueVariableCircular() =
    let context = { evaluationContext with Variables = Map ["toto", Expr.Variable "titi"
                                                            "titi", Expr.Variable "toto"] }
    
    (fun () -> eval context (Expr.Variable "toto") |> ignore)
    |> should (throwWithMessage "Variable toto has circular definition") typeof<TerrabuildException>

[<Test>]
let concatString() =
    let expected = Value.String "hello world"
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Plus, [Expr.String "hello"; Expr.String " world"]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let addNumber() =
    let expected = Value.Number 7
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Plus, [Expr.Number 5; Expr.Number 2]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let addMap() =
    let expected = Value.Map (Map [ "toto", Value.Number 42
                                    "titi", Value.String "pouet" ])
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Plus, [ Expr.Map (Map [ "toto", Expr.String "pouet"
                                                                                                  "titi", Expr.String "pouet" ])
                                                                                  Expr.Map (Map [ "toto", Expr.Number 42
                                                                                                  "titi", Expr.String "pouet" ]) ]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let addMapNothing() =
    let expected = Value.Map (Map [ "toto", Value.Number 42
                                    "titi", Value.String "pouet" ])
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Plus, [ Expr.Map (Map [ "toto", Expr.Number 42
                                                                                                  "titi", Expr.String "pouet" ])
                                                                                  Expr.Nothing ]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let addList() =
    let expected = Value.List ([ Value.String "toto"; Value.Number 42 ])
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Plus, [ Expr.List [Expr.String "toto"]
                                                                                  Expr.List [Expr.Number 42] ]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let addListNothing() =
    let expected = Value.List ([ Value.String "toto" ])
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Plus, [ Expr.List [Expr.String "toto"]
                                                                                  Expr.Nothing ]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let subNumber() =
    let expected = Value.Number 3
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Minus, [Expr.Number 5; Expr.Number 2]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let coalesce() =
    let expected = Value.Number 42
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Coalesce, [Expr.Nothing; Expr.Number 42]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let trimString() =
    let expected = Value.String "hello"
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Trim, [Expr.String " hello  "]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let upperString() =
    let expected = Value.String "HELLO"
    let varUsed, result =
        eval evaluationContext (Expr.Function (Function.Trim,
                                               [Expr.Function (Function.Upper, [ Expr.String " hello  " ])] ))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let lowerString() =
    let expected = Value.String "hello"
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Lower, [ Expr.String "HELLO" ]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let version() =
    let expected = Value.String "1234"

    let context = { evaluationContext
                    with Versions = Map [ "toto", "1234"
                                          "titi", "56789A" ] }

    printfn $"{context.ProjectDir}"

    let varUsed, result =
        eval context (Expr.Function (Function.Version, [ Expr.String "../net8.0/toto"]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let format() =
    let expected = Value.String "\\o/42truetiti"
    let expectedUsedVars = Set [ "toto" ]

    let context = { evaluationContext
                    with Variables = Map ["toto", Expr.String "\\o/"] }

    let varUsed, result =
        eval context (Expr.Function (Function.Format, [
            Expr.Variable "toto"
            Expr.Number 42
            Expr.Nothing
            Expr.Bool true
            Expr.String "titi" ]))
    varUsed |> should equal expectedUsedVars
    result |> should equal expected


[<Test>]
let listItem() =
    let expected = Value.Number 42
    let expectedUsedVars = Set ["tagada"]

    let context = { evaluationContext
                    with Variables = Map [ 
                        "tagada", Expr.List [ Expr.String "toto"; Expr.Number 42 ]
                    ] }

    let varUsed, result =
        eval context (Expr.Function (Function.Item, [ Expr.Variable "tagada"; Expr.Number 1]))
    varUsed |> should equal expectedUsedVars
    result |> should equal expected

[<Test>]
let listTryItem() =
    let expected = Value.Nothing
    let expectedUsedVars = Set ["tagada"]

    let context = { evaluationContext
                    with Variables = Map [ 
                        "tagada", Expr.List [ Expr.String "toto"; Expr.Number 42 ]
                    ] }

    let varUsed, result =
        eval context (Expr.Function (Function.TryItem, [ Expr.Variable "tagada"; Expr.Number 3]))
    varUsed |> should equal expectedUsedVars
    result |> should equal expected

[<Test>]
let mapItem() =
    let expected = Value.Number 42
    let expectedUsedVars = Set ["tagada"]

    let context = { evaluationContext
                    with Variables = Map [ 
                        "tagada", Expr.Map (Map [ "toto", Expr.Number 42 ])
                    ] }

    let varUsed, result =
        eval context (Expr.Function (Function.Item, [ Expr.Variable "tagada"; Expr.String "toto" ]))
    varUsed |> should equal expectedUsedVars
    result |> should equal expected

[<Test>]
let mapTryItem() =
    let expected = Value.Nothing
    let expectedUsedVars = Set ["tagada"]

    let context = { evaluationContext
                    with Variables = Map [ 
                        "tagada", Expr.Map (Map [ "toto", Expr.Number 42 ])
                    ] }

    let varUsed, result =
        eval context (Expr.Function (Function.TryItem, [ Expr.Variable "tagada"; Expr.String "titi" ]))
    varUsed |> should equal expectedUsedVars
    result |> should equal expected

[<Test>]
let equalValue() =
    let expected = Value.Bool true
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Equal, [Expr.String "toto"; Expr.String "toto"]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let notEqualValue() =
    let expected = Value.Bool false
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Equal, [Expr.String "toto"; Expr.Number 42]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let replaceString() =
    let expected = Value.String "titi titi"
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Replace, [Expr.String "toto titi"; Expr.String "toto"; Expr.String "titi"]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let countList() =
    let expected = Value.Number 3
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Count, [Expr.List [Expr.Number 1; Expr.String "toto"; Expr.Bool false]]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let countMap() =
    let expected = Value.Number 1
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Count, [Expr.Map (Map [ "toto", Expr.Number 42 ])]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let notBool() =
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Not, [Expr.Bool false]))
    varUsed |> should be Empty
    result |> should equal (Value.Bool true)

    let varUsed, result = eval evaluationContext (Expr.Function (Function.Not, [Expr.Bool true]))
    varUsed |> should be Empty
    result |> should equal (Value.Bool false)

[<Test>]
let notNothing() =
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Not, [Expr.Nothing]))
    varUsed |> should be Empty
    result |> should equal (Value.Bool true)

[<Test>]
let notAnything() =
    let varUsed, result = eval evaluationContext (Expr.Function (Function.Not, [Expr.Number 42]))
    varUsed |> should be Empty
    result |> should equal (Value.Bool false)
