module FrontEnd
open FSharp.Text.Lexing

let dumpToken (lexbuff: LexBuffer<char>) =
    let token = Lexer.token lexbuff
    printfn $"TOKEN = {token}"
    token


let parse txt =
    let lexbuf = LexBuffer<_>.FromString txt
    try
        Parser.Configuration dumpToken lexbuf
    with
    | exn ->
        let err = sprintf "Unexpected token '%s' at (%d,%d)" 
                          (LexBuffer<_>.LexemeString lexbuf |> string) 
                          (lexbuf.StartPos.Column + 1) (lexbuf.StartPos.Line + 1) 
        failwith err
