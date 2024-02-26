// Signature file for parser generated by fsyacc
module Parser
type token = 
  | EOF
  | TRIM
  | UPPER
  | LOWER
  | PLUS
  | COMMA
  | EQUAL
  | LPAREN
  | RPAREN
  | LSQBRACKET
  | RSQBRACKET
  | LBRACE
  | RBRACE
  | VARIABLE of (string)
  | IDENTIFIER of (string)
  | STRING of (string)
  | NOTHING
  | TRUE
  | FALSE
type tokenId = 
    | TOKEN_EOF
    | TOKEN_TRIM
    | TOKEN_UPPER
    | TOKEN_LOWER
    | TOKEN_PLUS
    | TOKEN_COMMA
    | TOKEN_EQUAL
    | TOKEN_LPAREN
    | TOKEN_RPAREN
    | TOKEN_LSQBRACKET
    | TOKEN_RSQBRACKET
    | TOKEN_LBRACE
    | TOKEN_RBRACE
    | TOKEN_VARIABLE
    | TOKEN_IDENTIFIER
    | TOKEN_STRING
    | TOKEN_NOTHING
    | TOKEN_TRUE
    | TOKEN_FALSE
    | TOKEN_end_of_input
    | TOKEN_error
type nonTerminalId = 
    | NONTERM__startConfiguration
    | NONTERM_Configuration
    | NONTERM_Blocks
    | NONTERM_Block
    | NONTERM_BlockBody
    | NONTERM_BlockAttributes
    | NONTERM_Attribute
    | NONTERM_AttributeValue
    | NONTERM_AttributeArray
    | NONTERM_ArrayValues
    | NONTERM_AttributeSubBlock
    | NONTERM_AttributeMapValues
    | NONTERM_Expr
/// This function maps tokens to integer indexes
val tagOfToken: token -> int

/// This function maps integer indexes to symbolic token ids
val tokenTagToTokenId: int -> tokenId

/// This function maps production indexes returned in syntax errors to strings representing the non terminal that would be produced by that production
val prodIdxToNonTerminal: int -> nonTerminalId

/// This function gets the name of a token as a string
val token_to_string: token -> string
val Configuration : (FSharp.Text.Lexing.LexBuffer<'cty> -> token) -> FSharp.Text.Lexing.LexBuffer<'cty> -> (AST.Blocks) 
