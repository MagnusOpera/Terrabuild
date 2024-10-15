// Signature file for parser generated by fsyacc
module internal Terrabuild.Configuration.Project.Parser
type token = 
  | DEPENDENCIES
  | LINKS
  | OUTPUTS
  | IGNORES
  | INCLUDES
  | LABELS
  | VARIABLES
  | CONTAINER
  | INIT
  | SCRIPT
  | DEPENDS_ON
  | REBUILD
  | DEFAULTS
  | NAME
  | PROJECT
  | EXTENSION
  | TARGET
  | EOF
  | TRIM
  | UPPER
  | LOWER
  | REPLACE
  | VERSION
  | DOUBLE_QUESTION
  | QUESTION
  | COLON
  | MINUS
  | PLUS
  | COMMA
  | EQUAL
  | NOT_EQUAL
  | LPAREN
  | RPAREN
  | DOT
  | DOT_QUESTION
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
    | TOKEN_DEPENDENCIES
    | TOKEN_LINKS
    | TOKEN_OUTPUTS
    | TOKEN_IGNORES
    | TOKEN_INCLUDES
    | TOKEN_LABELS
    | TOKEN_VARIABLES
    | TOKEN_CONTAINER
    | TOKEN_INIT
    | TOKEN_SCRIPT
    | TOKEN_DEPENDS_ON
    | TOKEN_REBUILD
    | TOKEN_DEFAULTS
    | TOKEN_NAME
    | TOKEN_PROJECT
    | TOKEN_EXTENSION
    | TOKEN_TARGET
    | TOKEN_EOF
    | TOKEN_TRIM
    | TOKEN_UPPER
    | TOKEN_LOWER
    | TOKEN_REPLACE
    | TOKEN_VERSION
    | TOKEN_DOUBLE_QUESTION
    | TOKEN_QUESTION
    | TOKEN_COLON
    | TOKEN_MINUS
    | TOKEN_PLUS
    | TOKEN_COMMA
    | TOKEN_EQUAL
    | TOKEN_NOT_EQUAL
    | TOKEN_LPAREN
    | TOKEN_RPAREN
    | TOKEN_DOT
    | TOKEN_DOT_QUESTION
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
    | NONTERM__startProjectFile
    | NONTERM_ProjectFile
    | NONTERM_ProjectFileComponents
    | NONTERM_Extension
    | NONTERM_ExtensionComponents
    | NONTERM_ExtensionContainer
    | NONTERM_ExtensionVariables
    | NONTERM_ExtensionScript
    | NONTERM_ExtensionDefaults
    | NONTERM_Project
    | NONTERM_ProjectComponents
    | NONTERM_ProjectDependencies
    | NONTERM_ProjectLinks
    | NONTERM_ProjectOutputs
    | NONTERM_ProjectIgnores
    | NONTERM_ProjectIncludes
    | NONTERM_ProjectLabels
    | NONTERM_Target
    | NONTERM_TargetComponents
    | NONTERM_TargetDependsOn
    | NONTERM_TargetRebuild
    | NONTERM_TargetOutputs
    | NONTERM_TargetStep
    | NONTERM_Expr
    | NONTERM_TargetIdentifier
    | NONTERM_ExtensionIdentifier
    | NONTERM_Bool
    | NONTERM_String
    | NONTERM_ExprList
    | NONTERM_ExprListContent
    | NONTERM_ExprMap
    | NONTERM_ExprMapContent
    | NONTERM_ListOfString
    | NONTERM_Strings
    | NONTERM_ListOfIdentifiers
    | NONTERM_Identifiers
    | NONTERM_ListOfTargetIdentifiers
    | NONTERM_TargetIdentifiers
/// This function maps tokens to integer indexes
val tagOfToken: token -> int

/// This function maps integer indexes to symbolic token ids
val tokenTagToTokenId: int -> tokenId

/// This function maps production indexes returned in syntax errors to strings representing the non terminal that would be produced by that production
val prodIdxToNonTerminal: int -> nonTerminalId

/// This function gets the name of a token as a string
val token_to_string: token -> string
val ProjectFile : (FSharp.Text.Lexing.LexBuffer<'cty> -> token) -> FSharp.Text.Lexing.LexBuffer<'cty> -> (Terrabuild.Configuration.Project.AST.ProjectFile) 
