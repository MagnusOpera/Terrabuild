module internal Terrabuild.Configuration.Project.Lexer

open Terrabuild.Configuration.Project.Parser  // we need the terminal tokens from the Parser
open FSharp.Text.Lexing
open System.Text/// Rule token
val token: lexbuf: LexBuffer<char> -> token
/// Rule singleLineComment
val singleLineComment: lexbuf: LexBuffer<char> -> token
/// Rule interpolatedString
val interpolatedString: acc: StringBuilder -> lexbuf: LexBuffer<char> -> token
/// Rule interpolatedExpression
val interpolatedExpression: lexbuf: LexBuffer<char> -> token
