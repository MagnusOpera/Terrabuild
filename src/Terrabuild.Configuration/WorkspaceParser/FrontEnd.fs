module FrontEnd.Workspace
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
            | LexerMode.Default -> Lexer.Workspace.token
            | LexerMode.InterpolatedString -> Lexer.Workspace.interpolatedString (StringBuilder())
            | LexerMode.InterpolatedExpression -> Lexer.Workspace.interpolatedExpression

        let token = lexer lexbuff
        // printfn $"### SwitchableLexer  mode: {lexerMode}  token: {token}"

        match token with
        | Parser.Workspace.STRING_START -> lexerMode <- LexerMode.InterpolatedString
        | Parser.Workspace.STRING_END _ -> lexerMode <- LexerMode.Default
        | Parser.Workspace.EXPRESSION_START _ -> lexerMode <- LexerMode.InterpolatedExpression
        | Parser.Workspace.EXPRESSION_END -> lexerMode <- LexerMode.InterpolatedString
        | _ -> ()
        token

    let lexbuf = LexBuffer<_>.FromString txt
    try
        Parser.Workspace.WorkspaceFile switchableLexer lexbuf
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
