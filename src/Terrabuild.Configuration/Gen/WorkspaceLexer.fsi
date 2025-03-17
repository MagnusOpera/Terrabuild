module internal Lexer.Workspace

open Parser.Workspace  // we need the terminal tokens from the Parser
open FSharp.Text.Lexing
open System.Text
open Errors/// Rule token
val token: lexbuf: LexBuffer<char> -> token
/// Rule singleLineComment
val singleLineComment: lexbuf: LexBuffer<char> -> token
/// Rule interpolatedString
val interpolatedString: acc: StringBuilder -> lexbuf: LexBuffer<char> -> token
/// Rule interpolatedExpression
val interpolatedExpression: lexbuf: LexBuffer<char> -> token
