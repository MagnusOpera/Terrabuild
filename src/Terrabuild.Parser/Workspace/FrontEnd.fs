module FrontEnd
open FSharp.Text.Lexing

let dumpToken (lexbuff: LexBuffer<char>) =
    let token = WorkspaceLexer.token lexbuff
    printfn $"TOKEN = {token}"
    token


let parse txt =
    let lexbuf = LexBuffer<_>.FromString txt
    try
        WorkspaceParser.Workspace dumpToken lexbuf
    with
    | _ ->
        let err = sprintf "Unexpected token '%s' at (%d,%d)"
                          (LexBuffer<_>.LexemeString lexbuf |> string) 
                          (lexbuf.StartPos.Column + 1) (lexbuf.StartPos.Line + 1) 
        failwith err
