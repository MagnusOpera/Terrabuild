module Terrabuild.Expressions.Tests

open NUnit.Framework
open FsUnit
open Eval

let private evaluationContext = {
    Eval.EvaluationContext.WorkspaceDir = TestContext.CurrentContext.WorkDirectory
    Eval.EvaluationContext.ProjectDir = TestContext.CurrentContext.TestDirectory
    Eval.EvaluationContext.Variables = Map.empty
    Eval.EvaluationContext.Versions = Map.empty
}

[<Test>]
let valueNothing() =
    let expected = Value.Nothing
    let result = eval evaluationContext (Expr.Nothing)
    result |> should equal expected

[<Test>]
let valueString() =
    let expected = Value.String "toto"
    let result = eval evaluationContext (Expr.String "toto")
    result |> should equal expected

[<Test>]
let valueBool() =
    let expected = Value.Bool true
    let result = eval evaluationContext (Expr.Boolean true)
    result |> should equal expected

[<Test>]
let valueMap() =
    let expected = Value.Map (Map ["hello", Value.String "world"])
    let result = eval evaluationContext (Expr.Map (Map ["hello", Expr.String "world"]))
    result |> should equal expected

[<Test>]
let valueVariable() =
    let expected = Value.String "titi"
    let context = { evaluationContext with Variables = Map ["toto", Expr.String "titi"] }
    let result = eval context (Expr.Variable "toto")
    result |> should equal expected

[<Test>]
let concatString() =
    let expected = Value.String "hello world"
    let result = eval evaluationContext (Expr.Function (Function.Plus, [Expr.String "hello"; Expr.String " world"]))
    result |> should equal expected

[<Test>]
let trimString() =
    let expected = Value.String "hello"
    let result = eval evaluationContext (Expr.Function (Function.Trim, [Expr.String " hello  "]))
    result |> should equal expected

[<Test>]
let upperString() =
    let expected = Value.String "HELLO"
    let result = eval evaluationContext (Expr.Function (Function.Trim,
                                                        [Expr.Function (Function.Upper, [ Expr.String " hello  " ])] ))
    result |> should equal expected

[<Test>]
let lowerString() =
    let expected = Value.String "hello"
    let result = eval evaluationContext (Expr.Function (Function.Lower, [ Expr.String "HELLO" ]))
    result |> should equal expected

[<Test>]
let version() =
    let expected = Value.String "1234"

    let context = { evaluationContext
                    with Versions = Map [ "toto", "1234"
                                          "titi", "56789A" ] }

    printfn $"{context.ProjectDir}"

    let result = eval context (Expr.Function (Function.Version, [ Expr.String "../net8.0/toto"]))
    result |> should equal expected
