module internal Terrabuild.Lang.Lexer

open Terrabuild.Lang.Parser  // we need the terminal tokens from the Parser
open FSharp.Text.Lexing
open System.Text
open Errors
open System.Collections.Generic/// Rule token
val token: lexerMode: obj -> lexbuf: LexBuffer<char> -> token
/// Rule singleLineComment
val singleLineComment: lexerMode: obj -> lexbuf: LexBuffer<char> -> token
/// Rule interpolatedString
val interpolatedString: acc: StringBuilder -> lexerMode: obj -> lexbuf: LexBuffer<char> -> token
