module Terrabuild.Configuration.Workspace.FrontEnd
open FSharp.Text.Lexing
open Errors
open System.Text


[<RequireQualifiedAccess>]
type LexerMode =
    | Default
    | InterpolatedString
    | InterpolatedExpression


let parse txt = 
    let mutable lexerMode = LexerMode.Default
    let switchableLexer (lexbuff: LexBuffer<char>) =
        let lexer =
            match lexerMode with
            | LexerMode.Default -> Lexer.token
            | LexerMode.InterpolatedString -> Lexer.interpolatedString (StringBuilder())
            | LexerMode.InterpolatedExpression -> Lexer.interpolatedExpression

        let token = lexer lexbuff
        printfn $"### SwitchableLexer  mode: {lexerMode}  token: {token}"

        match token with
        | Parser.STRING_START -> lexerMode <- LexerMode.InterpolatedString
        | Parser.STRING_END _ -> lexerMode <- LexerMode.Default
        | Parser.EXPRESSION_START _ -> lexerMode <- LexerMode.InterpolatedExpression
        | Parser.EXPRESSION_END -> lexerMode <- LexerMode.InterpolatedString
        | _ -> ()
        token

    let lexbuf = LexBuffer<_>.FromString txt
    try
        Parser.WorkspaceFile switchableLexer lexbuf
    with
    | exn ->
        let err = sprintf "Unexpected token '%s' at (%d,%d)"
                          (LexBuffer<_>.LexemeString lexbuf |> string) 
                          (lexbuf.StartPos.Line + 1) (lexbuf.StartPos.Column + 1)
        raiseParseError err


