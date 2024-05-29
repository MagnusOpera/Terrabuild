module Terrabuild.Configuration.FrontEnd
open FSharp.Text.Lexing
open Errors


let inline private dumpLexer lexer (lexbuff: LexBuffer<char>) =
    let token = lexer lexbuff
    // printfn $"TOKEN = {token}"
    token


let private parse parser lexer txt =
    let lexbuf = LexBuffer<_>.FromString txt
    try
        parser (dumpLexer lexer) lexbuf
    with
    | exn ->
        let err = sprintf "Unexpected token '%s' at (%d,%d)"
                          (LexBuffer<_>.LexemeString lexbuf |> string) 
                          (lexbuf.StartPos.Line + 1) (lexbuf.StartPos.Column + 1)
        TerrabuildException.Raise(err)


let parseProject = parse Project.Parser.ProjectFile Project.Lexer.token

let parseWorkspace = parse Workspace.Parser.WorkspaceFile Workspace.Lexer.token
