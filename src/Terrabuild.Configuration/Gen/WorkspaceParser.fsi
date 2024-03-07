// Signature file for parser generated by fsyacc
module internal Terrabuild.Configuration.Workspace.Parser
type token = 
  | DEPENDS_ON
  | VARIABLES
  | CONTAINER
  | INIT
  | SCRIPT
  | DEFAULTS
  | TARGET
  | ENVIRONMENT
  | EXTENSION
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
    | TOKEN_DEPENDS_ON
    | TOKEN_VARIABLES
    | TOKEN_CONTAINER
    | TOKEN_INIT
    | TOKEN_SCRIPT
    | TOKEN_DEFAULTS
    | TOKEN_TARGET
    | TOKEN_ENVIRONMENT
    | TOKEN_EXTENSION
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
    | NONTERM__startWorkspace
    | NONTERM_Workspace
    | NONTERM_WorkspaceComponents
    | NONTERM_Target
    | NONTERM_TargetComponents
    | NONTERM_TargetDependsOn
    | NONTERM_Environment
    | NONTERM_EnvironmentComponents
    | NONTERM_EnvironmentVariables
    | NONTERM_Extension
    | NONTERM_ExtensionComponents
    | NONTERM_Container
    | NONTERM_Script
    | NONTERM_Defaults
    | NONTERM_String
    | NONTERM_ListOfString
    | NONTERM_Strings
    | NONTERM_ListOfTargets
    | NONTERM_Targets
    | NONTERM_StringVariables
    | NONTERM_StringVariable
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
val Workspace : (FSharp.Text.Lexing.LexBuffer<'cty> -> token) -> FSharp.Text.Lexing.LexBuffer<'cty> -> (Terrabuild.Configuration.Workspace.AST.Workspace) 
