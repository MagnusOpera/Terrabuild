module FrontEnd.Project
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
            | LexerMode.Default -> Lexer.Project.token
            | LexerMode.InterpolatedString -> Lexer.Project.interpolatedString (StringBuilder())
            | LexerMode.InterpolatedExpression -> Lexer.Project.interpolatedExpression

        let token = lexer lexbuff
        // printfn $"### SwitchableLexer  mode: {lexerMode}  token: {token}"

        match token with
        | Parser.Project.STRING_START -> lexerMode <- LexerMode.InterpolatedString
        | Parser.Project.STRING_END _ -> lexerMode <- LexerMode.Default
        | Parser.Project.EXPRESSION_START _ -> lexerMode <- LexerMode.InterpolatedExpression
        | Parser.Project.EXPRESSION_END -> lexerMode <- LexerMode.InterpolatedString
        | _ -> ()
        token

    let lexbuf = LexBuffer<_>.FromString txt
    try
        Parser.Project.ProjectFile switchableLexer lexbuf
    with
    | :? TerrabuildException as exn ->
        let err = sprintf "Parse error at (%d,%d)"
                          (lexbuf.StartPos.Line + 1) (lexbuf.StartPos.Column + 1)
        raiseParserError err exn
    | exn ->
        let err = sprintf "Unexpected token '%s' at (%d,%d)"
                          (LexBuffer<_>.LexemeString lexbuf |> string) 
                          (lexbuf.StartPos.Line + 1) (lexbuf.StartPos.Column + 1)
        raiseParserError err exn
