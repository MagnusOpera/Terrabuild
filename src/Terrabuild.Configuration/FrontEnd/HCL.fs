module FrontEnd.HCL

open FSharp.Text.Lexing
open Errors
open System.Text
open System.Collections.Generic


let parse txt =
    let lexerMode = Stack([Lexer.HCL.LexerMode.Default])

    let switchableLexer (lexbuff: LexBuffer<char>) =
        let mode = lexerMode |> Lexer.HCL.peek
        let lexer = 
            match mode with
            | Lexer.HCL.LexerMode.Default -> Lexer.HCL.token lexerMode
            | Lexer.HCL.LexerMode.String -> Lexer.HCL.interpolatedString (StringBuilder()) lexerMode

        let token = lexer lexbuff
        // printfn $"### SwitchableLexer  mode: {mode}  token: {token}"
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

