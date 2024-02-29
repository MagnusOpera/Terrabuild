module FrontEnd
open FSharp.Text.Lexing


let dumpLexer lexer (lexbuff: LexBuffer<char>) =
    let token = lexer lexbuff
    printfn $"TOKEN = {token}"
    token

let private parse parser lexer txt =
    let lexbuf = LexBuffer<_>.FromString txt
    try
        parser (dumpLexer lexer) lexbuf
    with
    | exn ->
        let err = sprintf "Unexpected token '%s' at (%d,%d)"
                          (LexBuffer<_>.LexemeString lexbuf |> string) 
                          (lexbuf.StartPos.Column + 1) (lexbuf.StartPos.Line + 1)
        failwith err


let parseProject = parse ProjectParser.Project ProjectLexer.token

let parseWorkspace = parse WorkspaceParser.Workspace WorkspaceLexer.token
