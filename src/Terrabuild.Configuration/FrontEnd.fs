module Terrabuild.Configuration.FrontEnd
open FSharp.Text.Lexing


let private dumpLexer lexer (lexbuff: LexBuffer<char>) =
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
        failwith err


let parseProject = parse Project.Parser.Project Project.Lexer.token

let parseWorkspace = parse Workspace.Parser.Workspace Workspace.Lexer.token
