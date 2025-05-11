module Terrabuild.Lang.Tests
open Terrabuild.Lang.AST
open Terrabuild.Expressions
open System.IO
open NUnit.Framework
open FsUnit

[<Test>]
let checkSyntax() =
    let expected = 
        { File.Blocks =
            [ { Block.Resource = "toplevelblock"
                Block.Name = None
                Block.Id = None
                Block.Attributes = [ { Attribute.Name = "attribute1"; Attribute.Value = Expr.String "42" }
                                     { Attribute.Name = "attribute2"; Attribute.Value = Expr.Variable "local.value" }]
                Block.Blocks = [ { Block.Resource = "innerblock"
                                   Block.Name = None
                                   Block.Id = None
                                   Block.Attributes = [ { Attribute.Name = "innerattribute"; Value = Expr.Number 666 }]
                                   Block.Blocks = [] }
                                 { Block.Resource = "innerblockWithType"
                                   Block.Name = Some "type"
                                   Block.Id = None
                                   Block.Attributes = [ { Attribute.Name = "inner-attribute"; Attribute.Value = Expr.Number -20 }]
                                   Block.Blocks = [] } ] }
              { Block.Resource = "other_block_with_type"
                Block.Name = Some "type"
                Block.Id = None
                Block.Attributes = []
                Block.Blocks = [] }
              { Block.Resource = "other_block_with_type_name"
                Block.Name = Some "type"
                Block.Id = Some "name"
                Block.Attributes = []
                Block.Blocks = [] }
              { Block.Resource = "locals"
                Block.Name = None
                Block.Id = None
                Block.Attributes = [ { Attribute.Name = "string"; Attribute.Value = Expr.String "toto" }
                                     { Attribute.Name = "number"; Attribute.Value = Expr.Number 42 }
                                     { Attribute.Name = "negative_number"; Attribute.Value = Expr.Number -42 }
                                     { Attribute.Name = "map"; Attribute.Value = Expr.Map (Map [("a", Expr.Number 42)
                                                                                                ("b", Expr.Number 666)]) }
                                     { Attribute.Name = "list"; Attribute.Value = Expr.List [Expr.String "a"
                                                                                             Expr.String "b"] }
                                     { Attribute.Name = "literal_bool_true"; Attribute.Value = Expr.Bool true }
                                     { Attribute.Name = "literal_bool_false"; Attribute.Value = Expr.Bool false }
                                     { Attribute.Name = "literal_nothing"; Attribute.Value = Expr.Nothing }
                                     { Attribute.Name = "interpolated_string"; Attribute.Value = Expr.Function(Function.ToString,
                                                                                                               [Expr.Function (Function.Format,
                                                                                                                               [Expr.String "{0}{1}"
                                                                                                                                Expr.String "toto "
                                                                                                                                Expr.Function (Function.Plus,
                                                                                                                                               [Expr.Variable "local.var"; Expr.Number 42])])]) }
                                     { Attribute.Name = "data"; Value = Expr.Variable "var.titi" }
                                     { Attribute.Name = "data_index"; Value = Expr.Function (Function.Item, [Expr.Variable "var.toto"; Expr.Number 42]) }
                                     { Attribute.Name = "data_maybe_index"; Value = Expr.Function (Function.TryItem, [Expr.Variable "var.toto"; Expr.Number 42]) }
                                     { Attribute.Name = "data_index_name"; Value = Expr.Function (Function.Item, [Expr.Variable "var.toto"; Expr.String "field"]) }
                                     { Attribute.Name = "data_maybe_index_name"; Value = Expr.Function (Function.TryItem, [Expr.Variable "var.toto"; Expr.String "field"]) }
                                     { Attribute.Name = "bool_equal"; Value = Expr.Function (Function.Equal, [Expr.Number 42; Expr.Number 666]) }
                                     { Attribute.Name = "bool_not_equal"; Attribute.Value = Expr.Function (Function.NotEqual, [Expr.Number 42; Expr.Number 666]) }
                                     { Attribute.Name = "bool_and"; Value = Expr.Function (Function.And, [Expr.Bool true; Expr.Bool false]) }
                                     { Attribute.Name = "bool_or"; Attribute.Value = Expr.Function (Function.Or, [Expr.Bool true; Expr.Bool false]) }
                                     { Attribute.Name = "bool_not"; Attribute.Value = Expr.Function (Function.Not, [Expr.Bool false]) }
                                     { Attribute.Name = "expr_math_op"; Attribute.Value = Expr.Function(Function.Minus,
                                                                                                        [Expr.Function(Function.Plus,
                                                                                                                       [Expr.Function (Function.Plus,
                                                                                                                                       [Expr.Number 1
                                                                                                                                        Expr.Function (Function.Mult,
                                                                                                                                                       [Expr.Number 42
                                                                                                                                                        Expr.Number 2])])
                                                                                                                        Expr.Function (Function.Div,
                                                                                                                                       [Expr.Number 4
                                                                                                                                        Expr.Number 4])])
                                                                                                         Expr.Number 3]) }
                                     { Attribute.Name = "expr_bool_op"; Attribute.Value = Expr.Function(Function.Equal,
                                                                                                        [Expr.Function
                                                                                                            (Function.Equal,
                                                                                                             [Expr.Function (Function.Plus,
                                                                                                                             [Expr.Number 1
                                                                                                                              Expr.Number 42])
                                                                                                              Expr.Function (Function.Plus,
                                                                                                                             [Expr.Number 42
                                                                                                                              Expr.Number 1])])
                                                                                                         Expr.Bool false]) }
                                     { Attribute.Name = "coalesce_op"; Attribute.Value = Expr.Function (Function.Coalesce,
                                                                                                        [Expr.Nothing
                                                                                                         Expr.String "toto"]) }
                                     { Attribute.Name = "ternary_op"; Attribute.Value = Expr.Function (Function.Ternary,
                                                                                                       [Expr.Bool true
                                                                                                        Expr.String "titi"
                                                                                                        Expr.String "toto"]) }
                                     { Attribute.Name = "function_trim"; Attribute.Value = Expr.Function (Function.Trim, []) }
                                     { Attribute.Name = "function_upper"; Attribute.Value = Expr.Function (Function.Upper, []) }
                                     { Attribute.Name = "function_lower"; Attribute.Value = Expr.Function (Function.Lower, []) }
                                     { Attribute.Name = "function_replace"; Attribute.Value = Expr.Function (Function.Replace, []) }
                                     { Attribute.Name = "function_count"; Attribute.Value = Expr.Function (Function.Count, []) }
                                     { Attribute.Name = "function_arity0"; Attribute.Value = Expr.Function (Function.Trim, []) }
                                     { Attribute.Name = "function_arity1"; Attribute.Value = Expr.Function (Function.Trim, [Expr.String "titi"]) }
                                     { Attribute.Name = "function_arity2"; Attribute.Value = Expr.Function (Function.Trim, [Expr.String "titi"; Expr.Number 42]) }
                                     { Attribute.Name = "function_arity3"; Value = Expr.Function (Function.Trim, [Expr.String "titi"; Expr.Number 42; Expr.Bool false]) }]
                Blocks = [] }] }

    let content = File.ReadAllText("TestFiles/SYNTAX")
    let file = FrontEnd.parse content

    file |> should equal expected

[<Test>]
let parseProject() =
    let content = File.ReadAllText("TestFiles/PROJECT")
    let file = FrontEnd.parse content
    printfn $"{file}"

[<Test>]
let parseProject2() =
    let content = File.ReadAllText("TestFiles/PROJECT2")
    let file = FrontEnd.parse content
    printfn $"{file}"

[<Test>]
let parseWorkspace() =
    let content = File.ReadAllText("TestFiles/WORKSPACE")
    let file = FrontEnd.parse content
    printfn $"{file}"

[<Test>]
let parseWorkspace2() =
    let content = File.ReadAllText("TestFiles/WORKSPACE2")
    let file = FrontEnd.parse content
    printfn $"{file}"
