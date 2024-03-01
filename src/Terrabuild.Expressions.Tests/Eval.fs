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
