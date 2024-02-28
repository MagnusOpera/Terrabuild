module BuildFrontEnd
open FSharp.Text.Lexing

let dumpToken (lexbuff: LexBuffer<char>) =
    let token = BuildLexer.token lexbuff
    printfn $"TOKEN = {token}"
    token


let parse txt =
    let lexbuf = LexBuffer<_>.FromString txt
    try
        BuildParser.Build dumpToken lexbuf
    with
    | _ ->
        let err = sprintf "Unexpected token '%s' at (%d,%d)"
                          (LexBuffer<_>.LexemeString lexbuf |> string) 
                          (lexbuf.StartPos.Column + 1) (lexbuf.StartPos.Line + 1) 
        failwith err
