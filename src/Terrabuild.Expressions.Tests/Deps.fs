module Terrabuild.Expressions.Dependencies.Tests

open NUnit.Framework
open FsUnitTyped
open Terrabuild.Expressions

[<Test>]
let ``Scalar dependencies``() =
    let expr = Expr.String "toto"
    let expected = Set.empty

    let deps = Dependencies.find expr
    deps |> shouldEqual expected

[<Test>]
let ``Variable dependencies``() =
    let expr = Expr.Variable "toto"
    let expected = Set.singleton "toto"

    let deps = Dependencies.find expr
    deps |> shouldEqual expected

[<Test>]
let ``Variable inside map dependencies``() =
    let expr = Expr.Map (Map [ "toto_field", Expr.Variable "toto"
                               "titi_field", Expr.Variable "titi" ])
    let expected = Set [ "toto"; "titi" ]

    let deps = Dependencies.find expr
    deps |> shouldEqual expected

[<Test>]
let ``Variable inside list dependencies``() =
    let expr = Expr.List [Expr.Variable "toto"; Expr.Variable "titi"]
    let expected = Set [ "toto"; "titi" ]

    let deps = Dependencies.find expr
    deps |> shouldEqual expected

[<Test>]
let ``field dependencies``() =
    let expr = Expr.Function (Function.Item, [Expr.Variable "var"; Expr.String "toto"])
    let expected = Set [ "var.toto" ]

    let deps = Dependencies.find expr
    deps |> shouldEqual expected

[<Test>]
let ``maybe field dependencies``() =
    let expr = Expr.Function (Function.TryItem, [Expr.Variable "var"; Expr.String "toto"])
    let expected = Set [ "var.toto" ]

    let deps = Dependencies.find expr
    deps |> shouldEqual expected

[<Test>]
let ``function dependencies``() =
    let expr = Expr.Function (Function.Plus, [Expr.Variable "toto"; Expr.Variable "titi"])
    let expected = Set [ "toto"; "titi" ]

    let deps = Dependencies.find expr
    deps |> shouldEqual expected

[<Test>]
let ``function of field dependencies``() =
    let expr = Expr.Function (Function.Plus, [Expr.Function (Function.Item, [Expr.Variable "var"; Expr.String "toto"])
                                              Expr.Function (Function.Item, [Expr.Variable "local"; Expr.String "titi"])])
    let expected = Set ["var.toto"; "local.titi"]

    let deps = Dependencies.find expr
    deps |> shouldEqual expected
