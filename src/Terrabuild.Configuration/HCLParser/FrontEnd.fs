module FrontEnd.HCL
open FSharp.Text.Lexing
open Errors
open System.Text

[<RequireQualifiedAccess>]
type LexerMode =
    | Default
    | InterpolatedString
    | InterpolatedExpression


let parse txt = 
    let lexerMode = System.Collections.Generic.Stack([LexerMode.Default])
    let switchableLexer (lexbuff: LexBuffer<char>) =
        let mode = lexerMode.Peek()
        let lexer =
            match mode with
            | LexerMode.Default -> Lexer.HCL.token
            | LexerMode.InterpolatedString -> Lexer.HCL.interpolatedString (StringBuilder())
            | LexerMode.InterpolatedExpression -> Lexer.HCL.interpolatedExpression

        let token = lexer lexbuff
        printfn $"### SwitchableLexer  mode: {mode}  token: {token}"

        match token with
        | Parser.HCL.STRING_START -> lexerMode.Push(LexerMode.InterpolatedString)
        | Parser.HCL.STRING_END _ -> lexerMode.Pop() |> ignore
        | Parser.HCL.EXPRESSION_START _ -> lexerMode.Push(LexerMode.InterpolatedExpression)
        | Parser.HCL.EXPRESSION_END -> lexerMode.Pop() |> ignore
        | _ -> ()
        token

    let lexbuf = LexBuffer<_>.FromString txt
    try
        Parser.HCL.File switchableLexer lexbuf
    with
    | :? TerrabuildException as exn ->
        let err = sprintf "Parse error at (%d,%d)"
                          (lexbuf.StartPos.Line + 1) (lexbuf.StartPos.Column + 1)
        raiseParserError(err, exn)
    | exn ->
        let err = sprintf "Unexpected token '%s' at (%d,%d)"
                          (LexBuffer<_>.LexemeString lexbuf |> string) 
                          (lexbuf.StartPos.Line + 1) (lexbuf.StartPos.Column + 1)
        raiseParserError(err, exn)
