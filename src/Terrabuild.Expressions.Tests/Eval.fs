module Terrabuild.Expressions.Tests

open NUnit.Framework
open FsUnit
open Eval

let mkVar value =
    value, Set.empty

let private evaluationContext = {
    Eval.EvaluationContext.WorkspaceDir = TestContext.CurrentContext.WorkDirectory
    Eval.EvaluationContext.ProjectDir = FS.combinePath TestContext.CurrentContext.WorkDirectory "project-path" |> Some
    Eval.EvaluationContext.Variables = Map.empty
    Eval.EvaluationContext.Versions = Map.empty
}

[<Test>]
let valueNothing() =
    let expected = Value.Nothing
    let result, varUsed = eval evaluationContext (Expr.Nothing)
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let valueString() =
    let expected = Value.String "toto"
    let result, varUsed = eval evaluationContext (Expr.String "toto")
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let valueBool() =
    let expected = Value.Bool true
    let result, varUsed = eval evaluationContext (Expr.Bool true)
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let valueMap() =
    let expectedUsedVars = Set [ "toto" ]
    let expected = Value.Map (Map ["hello", Value.String "world"])
    let context = { evaluationContext with Variables = Map ["toto", (Value.String "world", Set.empty)] }
    let result, varUsed = eval context (Expr.Map (Map ["hello", Expr.Variable "toto"]))
    varUsed |> should equal expectedUsedVars
    result |> should equal expected

[<Test>]
let valueList() =
    let expectedUsedVars = Set [ "toto" ]
    let expected = Value.List [Value.String "hello"; Value.String "world"]
    let context = { evaluationContext with Variables = Map ["toto", (Value.String "world", Set.empty)] }
    let result, varUsed = eval context (Expr.List [Expr.String "hello"; Expr.Variable "toto"])
    varUsed |> should equal expectedUsedVars
    result |> should equal expected

[<Test>]
let valueVariable() =
    let expectedUsedVars = Set [ "toto" ]
    let expected = Value.String "titi"
    let context = { evaluationContext with Variables = Map ["toto", Value.String "titi" |> mkVar] }
    let result, varUsed = eval context (Expr.Variable "toto")
    varUsed |> should equal expectedUsedVars
    result |> should equal expected

[<Test>]
let concatString() =
    let expected = Value.String "hello world"
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Plus, [Expr.String "hello"; Expr.String " world"]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let addNumber() =
    let expected = Value.Number 7
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Plus, [Expr.Number 5; Expr.Number 2]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let addMap() =
    let expected = Value.Map (Map [ "toto", Value.Number 42
                                    "titi", Value.String "pouet" ])
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Plus, [ Expr.Map (Map [ "toto", Expr.String "pouet"
                                                                                                  "titi", Expr.String "pouet" ])
                                                                                  Expr.Map (Map [ "toto", Expr.Number 42
                                                                                                  "titi", Expr.String "pouet" ]) ]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let addList() =
    let expected = Value.List ([ Value.String "toto"; Value.Number 42 ])
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Plus, [ Expr.List [Expr.String "toto"]
                                                                                  Expr.List [Expr.Number 42] ]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let subNumber() =
    let expected = Value.Number 3
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Minus, [Expr.Number 5; Expr.Number 2]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let coalesce() =
    let expected = Value.Number 42
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Coalesce, [Expr.Nothing; Expr.Number 42]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let trimString() =
    let expected = Value.String "hello"
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Trim, [Expr.String " hello  "]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let upperString() =
    let expected = Value.String "HELLO"
    let result, varUsed =
        eval evaluationContext (Expr.Function (Function.Trim,
                                               [Expr.Function (Function.Upper, [ Expr.String " hello  " ])] ))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let lowerString() =
    let expected = Value.String "hello"
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Lower, [ Expr.String "HELLO" ]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let version() =
    let expected = Value.String "1234"

    let context = { evaluationContext
                    with Versions = Map [ "toto", "1234"
                                          "titi", "56789A" ] }

    printfn $"{context.ProjectDir}"

    let result, varUsed =
        eval context (Expr.Function (Function.Version, [ Expr.String "../toto"]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let formatList() =
    let expected = Value.String "\\o/THIS42ISAtrueTEMPLATEtiti"
    let expectedUsedVars = Set [ "toto" ]

    let context = { evaluationContext
                    with Variables = Map ["toto", Value.String "\\o/" |> mkVar] }

    // format("{0}THIS{1}IS{2}A{3}TEMPLATE{4}", $toto, 42, nothing, true, "titi")
    let result, varUsed =
        eval context (Expr.Function (Function.Format, [
            Expr.String "{0}THIS{1}IS{2}A{3}TEMPLATE{4}"
            Expr.Variable "toto"
            Expr.Number 42
            Expr.Nothing
            Expr.Bool true
            Expr.String "titi" ]))
    varUsed |> should equal expectedUsedVars
    result |> should equal expected

[<Test>]
let formatMap() =
    let expected = Value.String "THIS\\o/IS42ATEMPLATEtrue"
    let expectedUsedVars = Set [ "args" ]

    let context = { evaluationContext
                    with Variables = Map ["args", Value.Map (Map [ "string", Value.String "\\o/"
                                                                   "number", Value.Number 42
                                                                   "nothing", Value.Nothing
                                                                   "bool", Value.Bool true ]) |> mkVar ] }
 
    // format("THIS{string}IS{number}A{nothing}TEMPLATE{bool}", $args)
    let result, varUsed =
        eval context (Expr.Function (Function.Format, [
            Expr.String "THIS{string}IS{number}A{nothing}TEMPLATE{bool}"
            Expr.Variable "args" ]))
    varUsed |> should equal expectedUsedVars
    result |> should equal expected


[<Test>]
let listItem() =
    let expected = Value.Number 42
    let expectedUsedVars = Set ["tagada"]

    let context = { evaluationContext
                    with Variables = Map [ 
                        "tagada", Value.List [ Value.String "toto"; Value.Number 42 ] |> mkVar
                    ] }

    let result, varUsed =
        eval context (Expr.Function (Function.Item, [ Expr.Variable "tagada"; Expr.Number 1]))
    varUsed |> should equal expectedUsedVars
    result |> should equal expected

[<Test>]
let listTryItem() =
    let expected = Value.Nothing
    let expectedUsedVars = Set ["tagada"]

    let context = { evaluationContext
                    with Variables = Map [ 
                        "tagada", Value.List [ Value.String "toto"; Value.Number 42 ] |> mkVar
                    ] }

    let result, varUsed =
        eval context (Expr.Function (Function.TryItem, [ Expr.Variable "tagada"; Expr.Number 3]))
    varUsed |> should equal expectedUsedVars
    result |> should equal expected

[<Test>]
let mapItem() =
    let expected = Value.Number 42
    let expectedUsedVars = Set ["tagada"]

    let context = { evaluationContext
                    with Variables = Map [ 
                        "tagada", Value.Map (Map [ "toto", Value.Number 42 ]) |> mkVar
                    ] }

    let result, varUsed =
        eval context (Expr.Function (Function.Item, [ Expr.Variable "tagada"; Expr.String "toto" ]))
    varUsed |> should equal expectedUsedVars
    result |> should equal expected

[<Test>]
let mapTryItem() =
    let expected = Value.Nothing
    let expectedUsedVars = Set ["tagada"]

    let context = { evaluationContext
                    with Variables = Map [ 
                        "tagada", Value.Map (Map [ "toto", Value.Number 42 ]) |> mkVar
                    ] }

    let result, varUsed =
        eval context (Expr.Function (Function.TryItem, [ Expr.Variable "tagada"; Expr.String "titi" ]))
    varUsed |> should equal expectedUsedVars
    result |> should equal expected

[<Test>]
let equalValue() =
    let expected = Value.Bool true
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Equal, [Expr.String "toto"; Expr.String "toto"]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let notEqualValue() =
    let expected = Value.Bool false
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Equal, [Expr.String "toto"; Expr.Number 42]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let replaceString() =
    let expected = Value.String "titi titi"
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Replace, [Expr.String "toto titi"; Expr.String "toto"; Expr.String "titi"]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let countList() =
    let expected = Value.Number 3
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Count, [Expr.List [Expr.Number 1; Expr.String "toto"; Expr.Bool false]]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let countMap() =
    let expected = Value.Number 1
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Count, [Expr.Map (Map [ "toto", Expr.Number 42 ])]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let notBool() =
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Not, [Expr.Bool false]))
    varUsed |> should be Empty
    result |> should equal (Value.Bool true)

    let result, varUsed = eval evaluationContext (Expr.Function (Function.Not, [Expr.Bool true]))
    varUsed |> should be Empty
    result |> should equal (Value.Bool false)

[<Test>]
let notNothing() =
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Not, [Expr.Nothing]))
    varUsed |> should be Empty
    result |> should equal (Value.Bool true)

[<Test>]
let notAnything() =
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Not, [Expr.Number 42]))
    varUsed |> should be Empty
    result |> should equal (Value.Bool false)

[<Test>]
let andBool() =
    let expected = Value.Bool false
    let result, varUsed = eval evaluationContext (Expr.Function (Function.And, [Expr.Bool false; Expr.Bool false]))
    varUsed |> should be Empty
    result |> should equal expected

    let expected = Value.Bool false
    let result, varUsed = eval evaluationContext (Expr.Function (Function.And, [Expr.Bool false; Expr.Bool true]))
    varUsed |> should be Empty
    result |> should equal expected

    let expected = Value.Bool false
    let result, varUsed = eval evaluationContext (Expr.Function (Function.And, [Expr.Bool true; Expr.Bool false]))
    varUsed |> should be Empty
    result |> should equal expected

    let expected = Value.Bool true
    let result, varUsed = eval evaluationContext (Expr.Function (Function.And, [Expr.Bool true; Expr.Bool true]))
    varUsed |> should be Empty
    result |> should equal expected

[<Test>]
let orBool() =
    let expected = Value.Bool false
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Or, [Expr.Bool false; Expr.Bool false]))
    varUsed |> should be Empty
    result |> should equal expected

    let expected = Value.Bool true
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Or, [Expr.Bool false; Expr.Bool true]))
    varUsed |> should be Empty
    result |> should equal expected

    let expected = Value.Bool true
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Or, [Expr.Bool true; Expr.Bool false]))
    varUsed |> should be Empty
    result |> should equal expected

    let expected = Value.Bool true
    let result, varUsed = eval evaluationContext (Expr.Function (Function.Or, [Expr.Bool true; Expr.Bool true]))
    varUsed |> should be Empty
    result |> should equal expected
