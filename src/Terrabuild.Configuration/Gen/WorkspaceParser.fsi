// Signature file for parser generated by fsyacc
module internal Terrabuild.Configuration.Workspace.Parser
type token = 
  | SPACE
  | DEPENDS_ON
  | REBUILD
  | VARIABLES
  | CONTAINER
  | INIT
  | SCRIPT
  | DEFAULTS
  | WORKSPACE
  | TARGET
  | CONFIGURATION
  | EXTENSION
  | EOF
  | TRIM
  | UPPER
  | LOWER
  | VERSION
  | PLUS
  | COMMA
  | EQUAL
  | LPAREN
  | RPAREN
  | DOT_LSQBRACKET
  | LSQBRACKET
  | RSQBRACKET
  | LBRACE
  | RBRACE
  | NUMBER of (int)
  | KEY of (string)
  | VARIABLE of (string)
  | TARGET_IDENTIFIER of (string)
  | EXTENSION_IDENTIFIER of (string)
  | IDENTIFIER of (string)
  | STRING of (string)
  | NOTHING
  | TRUE
  | FALSE
type tokenId = 
    | TOKEN_SPACE
    | TOKEN_DEPENDS_ON
    | TOKEN_REBUILD
    | TOKEN_VARIABLES
    | TOKEN_CONTAINER
    | TOKEN_INIT
    | TOKEN_SCRIPT
    | TOKEN_DEFAULTS
    | TOKEN_WORKSPACE
    | TOKEN_TARGET
    | TOKEN_CONFIGURATION
    | TOKEN_EXTENSION
    | TOKEN_EOF
    | TOKEN_TRIM
    | TOKEN_UPPER
    | TOKEN_LOWER
    | TOKEN_VERSION
    | TOKEN_PLUS
    | TOKEN_COMMA
    | TOKEN_EQUAL
    | TOKEN_LPAREN
    | TOKEN_RPAREN
    | TOKEN_DOT_LSQBRACKET
    | TOKEN_LSQBRACKET
    | TOKEN_RSQBRACKET
    | TOKEN_LBRACE
    | TOKEN_RBRACE
    | TOKEN_NUMBER
    | TOKEN_KEY
    | TOKEN_VARIABLE
    | TOKEN_TARGET_IDENTIFIER
    | TOKEN_EXTENSION_IDENTIFIER
    | TOKEN_IDENTIFIER
    | TOKEN_STRING
    | TOKEN_NOTHING
    | TOKEN_TRUE
    | TOKEN_FALSE
    | TOKEN_end_of_input
    | TOKEN_error
type nonTerminalId = 
    | NONTERM__startWorkspaceFile
    | NONTERM_WorkspaceFile
    | NONTERM_WorkspaceFileComponents
    | NONTERM_Workspace
    | NONTERM_WorkspaceComponents
    | NONTERM_WorkspaceSpace
    | NONTERM_Target
    | NONTERM_TargetComponents
    | NONTERM_TargetDependsOn
    | NONTERM_TargetRebuild
    | NONTERM_Configuration
    | NONTERM_ConfigurationComponents
    | NONTERM_ConfigurationVariables
    | NONTERM_Extension
    | NONTERM_ExtensionComponents
    | NONTERM_ExtensionContainer
    | NONTERM_ExtensionVariables
    | NONTERM_ExtensionScript
    | NONTERM_ExtensionDefaults
    | NONTERM_Bool
    | NONTERM_String
    | NONTERM_ListOfString
    | NONTERM_Strings
    | NONTERM_ListOfTargetIdentifiers
    | NONTERM_TargetIdentifiers
    | NONTERM_TargetIdentifier
    | NONTERM_ExtensionIdentifier
    | NONTERM_Expr
    | NONTERM_ExprIndex
    | NONTERM_ExprList
    | NONTERM_ExprListContent
    | NONTERM_ExprMap
    | NONTERM_ExprMapContent
/// This function maps tokens to integer indexes
val tagOfToken: token -> int

/// This function maps integer indexes to symbolic token ids
val tokenTagToTokenId: int -> tokenId

/// This function maps production indexes returned in syntax errors to strings representing the non terminal that would be produced by that production
val prodIdxToNonTerminal: int -> nonTerminalId

/// This function gets the name of a token as a string
val token_to_string: token -> string
val WorkspaceFile : (FSharp.Text.Lexing.LexBuffer<'cty> -> token) -> FSharp.Text.Lexing.LexBuffer<'cty> -> (Terrabuild.Configuration.Workspace.AST.WorkspaceFile) 
