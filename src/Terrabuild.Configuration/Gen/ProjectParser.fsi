// Signature file for parser generated by fsyacc
module internal Terrabuild.Configuration.Project.Parser
type token = 
  | DEPENDENCIES
  | OUTPUTS
  | IGNORES
  | LABELS
  | CONTAINER
  | INIT
  | SCRIPT
  | DEPENDS_ON
  | DEFAULTS
  | EXTENSION
  | PROJECT
  | TARGET
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
  | KEY of (string)
  | VARIABLE of (string)
  | IDENTIFIER of (string)
  | STRING of (string)
  | NOTHING
  | TRUE
  | FALSE
type tokenId = 
    | TOKEN_DEPENDENCIES
    | TOKEN_OUTPUTS
    | TOKEN_IGNORES
    | TOKEN_LABELS
    | TOKEN_CONTAINER
    | TOKEN_INIT
    | TOKEN_SCRIPT
    | TOKEN_DEPENDS_ON
    | TOKEN_DEFAULTS
    | TOKEN_EXTENSION
    | TOKEN_PROJECT
    | TOKEN_TARGET
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
    | TOKEN_KEY
    | TOKEN_VARIABLE
    | TOKEN_IDENTIFIER
    | TOKEN_STRING
    | TOKEN_NOTHING
    | TOKEN_TRUE
    | TOKEN_FALSE
    | TOKEN_end_of_input
    | TOKEN_error
type nonTerminalId = 
    | NONTERM__startProject
    | NONTERM_Project
    | NONTERM_ProjectComponents
    | NONTERM_Extension
    | NONTERM_ExtensionComponents
    | NONTERM_Container
    | NONTERM_Script
    | NONTERM_Defaults
    | NONTERM_Configuration
    | NONTERM_ConfigurationComponents
    | NONTERM_ConfigurationDependencies
    | NONTERM_ConfigurationOutputs
    | NONTERM_ConfigurationIgnores
    | NONTERM_ConfigurationLabels
    | NONTERM_Target
    | NONTERM_TargetComponents
    | NONTERM_DependsOn
    | NONTERM_Step
    | NONTERM_String
    | NONTERM_ListOfString
    | NONTERM_Strings
    | NONTERM_ListOfTargets
    | NONTERM_Targets
    | NONTERM_Variables
    | NONTERM_Variable
    | NONTERM_Expr
/// This function maps tokens to integer indexes
val tagOfToken: token -> int

/// This function maps integer indexes to symbolic token ids
val tokenTagToTokenId: int -> tokenId

/// This function maps production indexes returned in syntax errors to strings representing the non terminal that would be produced by that production
val prodIdxToNonTerminal: int -> nonTerminalId

/// This function gets the name of a token as a string
val token_to_string: token -> string
val Project : (FSharp.Text.Lexing.LexBuffer<'cty> -> token) -> FSharp.Text.Lexing.LexBuffer<'cty> -> (Terrabuild.Configuration.Project.AST.Project) 
