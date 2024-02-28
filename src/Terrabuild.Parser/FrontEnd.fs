module FrontEnd
open FSharp.Text.Lexing


let private parse parser lexer txt =
    let lexbuf = LexBuffer<_>.FromString txt
    try
        parser lexer lexbuf
    with
    | _ ->
        let err = sprintf "Unexpected token '%s' at (%d,%d)"
                          (LexBuffer<_>.LexemeString lexbuf |> string) 
                          (lexbuf.StartPos.Column + 1) (lexbuf.StartPos.Line + 1) 
        failwith err


let parseBuild = parse BuildParser.Build BuildLexer.token

let parseWorkspace = parse WorkspaceParser.Workspace WorkspaceLexer.token
