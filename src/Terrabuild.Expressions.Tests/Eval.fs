module Terrabuild.Expressions.Tests

open NUnit.Framework
open FsUnit
open Eval

[<Test>]
let valueNothing() =
    let expected = Value.Nothing
    let result = eval Map.empty (Expr.Nothing)
    result |> should equal expected

[<Test>]
let valueString() =
    let expected = Value.String "toto"
    let result = eval Map.empty (Expr.String "toto")
    result |> should equal expected

[<Test>]
let valueBool() =
    let expected = Value.Bool true
    let result = eval Map.empty (Expr.Boolean true)
    result |> should equal expected

[<Test>]
let valueMap() =
    let expected = Value.Map (Map ["hello", Value.String "world"])
    let result = eval Map.empty (Expr.Map (Map ["hello", Expr.String "world"]))
    result |> should equal expected

[<Test>]
let valueVariable() =
    let expected = Value.String "titi"
    let result = eval (Map ["toto", "titi"]) (Expr.Variable "toto")
    result |> should equal expected

[<Test>]
let concatString() =
    let expected = Value.String "hello world"
    let result = eval Map.empty (Expr.Function (Function.Plus, [Expr.String "hello"; Expr.String " world"]))
    result |> should equal expected

[<Test>]
let trimString() =
    let expected = Value.String "hello"
    let result = eval Map.empty (Expr.Function (Function.Trim, [Expr.String " hello  "]))
    result |> should equal expected

[<Test>]
let upperString() =
    let expected = Value.String "HELLO"
    let result = eval Map.empty (Expr.Function (Function.Trim,
                                                [Expr.Function (Function.Upper, [ Expr.String " hello  " ])] ))
    result |> should equal expected

[<Test>]
let lowerString() =
    let expected = Value.String "hello"
    let result = eval Map.empty (Expr.Function (Function.Lower, [ Expr.String "HELLO" ]))
    result |> should equal expected
