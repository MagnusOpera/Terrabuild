// Implementation file for parser generated by fsyacc
module WorkspaceParser
#nowarn "64";; // turn off warnings that type variables used in production annotations are instantiated to concrete type
open FSharp.Text.Lexing
open FSharp.Text.Parsing.ParseHelpers
# 1 "Workspace/Parser.fsy"
 
open Terrabuild.Parser.AST
open Terrabuild.Parser.Workspace.AST


#if DEBUG
let debugPrint s = printfn "### %s" s
#else
let debugPrint s = ignore s
#endif


# 19 "Workspace/Parser.fs"
// This type is the type of tokens accepted by the parser
type token = 
  | STORAGE
  | SOURCECONTROL
  | DEPENDS_ON
  | VARIABLES
  | CONTAINER
  | PARAMETERS
  | SCRIPT
  | TERRABUILD
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
  | VARIABLE of (string)
  | IDENTIFIER of (string)
  | STRING of (string)
  | NOTHING
  | TRUE
  | FALSE
// This type is used to give symbolic names to token indexes, useful for error messages
type tokenId = 
    | TOKEN_STORAGE
    | TOKEN_SOURCECONTROL
    | TOKEN_DEPENDS_ON
    | TOKEN_VARIABLES
    | TOKEN_CONTAINER
    | TOKEN_PARAMETERS
    | TOKEN_SCRIPT
    | TOKEN_TERRABUILD
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
    | TOKEN_VARIABLE
    | TOKEN_IDENTIFIER
    | TOKEN_STRING
    | TOKEN_NOTHING
    | TOKEN_TRUE
    | TOKEN_FALSE
    | TOKEN_end_of_input
    | TOKEN_error
// This type is used to give symbolic names to token indexes, useful for error messages
type nonTerminalId = 
    | NONTERM__startWorkspace
    | NONTERM_Workspace
    | NONTERM_WorkspaceComponents
    | NONTERM_Terrabuild
    | NONTERM_TerrabuildComponents
    | NONTERM_TerrabuildStorage
    | NONTERM_TerrabuildSourceControl
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
    | NONTERM_Parameters
    | NONTERM_String
    | NONTERM_ListOfString
    | NONTERM_Strings
    | NONTERM_Variables
    | NONTERM_Variable
    | NONTERM_Expr

// This function maps tokens to integer indexes
let tagOfToken (t:token) = 
  match t with
  | STORAGE  -> 0 
  | SOURCECONTROL  -> 1 
  | DEPENDS_ON  -> 2 
  | VARIABLES  -> 3 
  | CONTAINER  -> 4 
  | PARAMETERS  -> 5 
  | SCRIPT  -> 6 
  | TERRABUILD  -> 7 
  | TARGET  -> 8 
  | ENVIRONMENT  -> 9 
  | EXTENSION  -> 10 
  | EOF  -> 11 
  | TRIM  -> 12 
  | UPPER  -> 13 
  | LOWER  -> 14 
  | PLUS  -> 15 
  | COMMA  -> 16 
  | EQUAL  -> 17 
  | LPAREN  -> 18 
  | RPAREN  -> 19 
  | LSQBRACKET  -> 20 
  | RSQBRACKET  -> 21 
  | LBRACE  -> 22 
  | RBRACE  -> 23 
  | VARIABLE _ -> 24 
  | IDENTIFIER _ -> 25 
  | STRING _ -> 26 
  | NOTHING  -> 27 
  | TRUE  -> 28 
  | FALSE  -> 29 

// This function maps integer indexes to symbolic token ids
let tokenTagToTokenId (tokenIdx:int) = 
  match tokenIdx with
  | 0 -> TOKEN_STORAGE 
  | 1 -> TOKEN_SOURCECONTROL 
  | 2 -> TOKEN_DEPENDS_ON 
  | 3 -> TOKEN_VARIABLES 
  | 4 -> TOKEN_CONTAINER 
  | 5 -> TOKEN_PARAMETERS 
  | 6 -> TOKEN_SCRIPT 
  | 7 -> TOKEN_TERRABUILD 
  | 8 -> TOKEN_TARGET 
  | 9 -> TOKEN_ENVIRONMENT 
  | 10 -> TOKEN_EXTENSION 
  | 11 -> TOKEN_EOF 
  | 12 -> TOKEN_TRIM 
  | 13 -> TOKEN_UPPER 
  | 14 -> TOKEN_LOWER 
  | 15 -> TOKEN_PLUS 
  | 16 -> TOKEN_COMMA 
  | 17 -> TOKEN_EQUAL 
  | 18 -> TOKEN_LPAREN 
  | 19 -> TOKEN_RPAREN 
  | 20 -> TOKEN_LSQBRACKET 
  | 21 -> TOKEN_RSQBRACKET 
  | 22 -> TOKEN_LBRACE 
  | 23 -> TOKEN_RBRACE 
  | 24 -> TOKEN_VARIABLE 
  | 25 -> TOKEN_IDENTIFIER 
  | 26 -> TOKEN_STRING 
  | 27 -> TOKEN_NOTHING 
  | 28 -> TOKEN_TRUE 
  | 29 -> TOKEN_FALSE 
  | 32 -> TOKEN_end_of_input
  | 30 -> TOKEN_error
  | _ -> failwith "tokenTagToTokenId: bad token"

/// This function maps production indexes returned in syntax errors to strings representing the non terminal that would be produced by that production
let prodIdxToNonTerminal (prodIdx:int) = 
  match prodIdx with
    | 0 -> NONTERM__startWorkspace 
    | 1 -> NONTERM_Workspace 
    | 2 -> NONTERM_WorkspaceComponents 
    | 3 -> NONTERM_WorkspaceComponents 
    | 4 -> NONTERM_WorkspaceComponents 
    | 5 -> NONTERM_WorkspaceComponents 
    | 6 -> NONTERM_WorkspaceComponents 
    | 7 -> NONTERM_Terrabuild 
    | 8 -> NONTERM_TerrabuildComponents 
    | 9 -> NONTERM_TerrabuildComponents 
    | 10 -> NONTERM_TerrabuildComponents 
    | 11 -> NONTERM_TerrabuildStorage 
    | 12 -> NONTERM_TerrabuildSourceControl 
    | 13 -> NONTERM_Target 
    | 14 -> NONTERM_TargetComponents 
    | 15 -> NONTERM_TargetComponents 
    | 16 -> NONTERM_TargetDependsOn 
    | 17 -> NONTERM_Environment 
    | 18 -> NONTERM_EnvironmentComponents 
    | 19 -> NONTERM_EnvironmentComponents 
    | 20 -> NONTERM_EnvironmentVariables 
    | 21 -> NONTERM_Extension 
    | 22 -> NONTERM_ExtensionComponents 
    | 23 -> NONTERM_ExtensionComponents 
    | 24 -> NONTERM_ExtensionComponents 
    | 25 -> NONTERM_ExtensionComponents 
    | 26 -> NONTERM_Container 
    | 27 -> NONTERM_Script 
    | 28 -> NONTERM_Parameters 
    | 29 -> NONTERM_String 
    | 30 -> NONTERM_ListOfString 
    | 31 -> NONTERM_Strings 
    | 32 -> NONTERM_Strings 
    | 33 -> NONTERM_Variables 
    | 34 -> NONTERM_Variables 
    | 35 -> NONTERM_Variable 
    | 36 -> NONTERM_Expr 
    | 37 -> NONTERM_Expr 
    | 38 -> NONTERM_Expr 
    | 39 -> NONTERM_Expr 
    | 40 -> NONTERM_Expr 
    | 41 -> NONTERM_Expr 
    | 42 -> NONTERM_Expr 
    | 43 -> NONTERM_Expr 
    | 44 -> NONTERM_Expr 
    | _ -> failwith "prodIdxToNonTerminal: bad production index"

let _fsyacc_endOfInputTag = 32 
let _fsyacc_tagOfErrorTerminal = 30

// This function gets the name of a token as a string
let token_to_string (t:token) = 
  match t with 
  | STORAGE  -> "STORAGE" 
  | SOURCECONTROL  -> "SOURCECONTROL" 
  | DEPENDS_ON  -> "DEPENDS_ON" 
  | VARIABLES  -> "VARIABLES" 
  | CONTAINER  -> "CONTAINER" 
  | PARAMETERS  -> "PARAMETERS" 
  | SCRIPT  -> "SCRIPT" 
  | TERRABUILD  -> "TERRABUILD" 
  | TARGET  -> "TARGET" 
  | ENVIRONMENT  -> "ENVIRONMENT" 
  | EXTENSION  -> "EXTENSION" 
  | EOF  -> "EOF" 
  | TRIM  -> "TRIM" 
  | UPPER  -> "UPPER" 
  | LOWER  -> "LOWER" 
  | PLUS  -> "PLUS" 
  | COMMA  -> "COMMA" 
  | EQUAL  -> "EQUAL" 
  | LPAREN  -> "LPAREN" 
  | RPAREN  -> "RPAREN" 
  | LSQBRACKET  -> "LSQBRACKET" 
  | RSQBRACKET  -> "RSQBRACKET" 
  | LBRACE  -> "LBRACE" 
  | RBRACE  -> "RBRACE" 
  | VARIABLE _ -> "VARIABLE" 
  | IDENTIFIER _ -> "IDENTIFIER" 
  | STRING _ -> "STRING" 
  | NOTHING  -> "NOTHING" 
  | TRUE  -> "TRUE" 
  | FALSE  -> "FALSE" 

// This function gets the data carried by a token as an object
let _fsyacc_dataOfToken (t:token) = 
  match t with 
  | STORAGE  -> (null : System.Object) 
  | SOURCECONTROL  -> (null : System.Object) 
  | DEPENDS_ON  -> (null : System.Object) 
  | VARIABLES  -> (null : System.Object) 
  | CONTAINER  -> (null : System.Object) 
  | PARAMETERS  -> (null : System.Object) 
  | SCRIPT  -> (null : System.Object) 
  | TERRABUILD  -> (null : System.Object) 
  | TARGET  -> (null : System.Object) 
  | ENVIRONMENT  -> (null : System.Object) 
  | EXTENSION  -> (null : System.Object) 
  | EOF  -> (null : System.Object) 
  | TRIM  -> (null : System.Object) 
  | UPPER  -> (null : System.Object) 
  | LOWER  -> (null : System.Object) 
  | PLUS  -> (null : System.Object) 
  | COMMA  -> (null : System.Object) 
  | EQUAL  -> (null : System.Object) 
  | LPAREN  -> (null : System.Object) 
  | RPAREN  -> (null : System.Object) 
  | LSQBRACKET  -> (null : System.Object) 
  | RSQBRACKET  -> (null : System.Object) 
  | LBRACE  -> (null : System.Object) 
  | RBRACE  -> (null : System.Object) 
  | VARIABLE _fsyacc_x -> Microsoft.FSharp.Core.Operators.box _fsyacc_x 
  | IDENTIFIER _fsyacc_x -> Microsoft.FSharp.Core.Operators.box _fsyacc_x 
  | STRING _fsyacc_x -> Microsoft.FSharp.Core.Operators.box _fsyacc_x 
  | NOTHING  -> (null : System.Object) 
  | TRUE  -> (null : System.Object) 
  | FALSE  -> (null : System.Object) 
let _fsyacc_gotos = [| 0us; 65535us; 1us; 65535us; 0us; 1us; 1us; 65535us; 0us; 2us; 1us; 65535us; 2us; 4us; 1us; 65535us; 9us; 10us; 1us; 65535us; 10us; 12us; 1us; 65535us; 10us; 13us; 1us; 65535us; 2us; 5us; 1us; 65535us; 22us; 23us; 1us; 65535us; 23us; 25us; 1us; 65535us; 2us; 6us; 1us; 65535us; 31us; 32us; 1us; 65535us; 32us; 34us; 1us; 65535us; 2us; 7us; 1us; 65535us; 41us; 42us; 1us; 65535us; 42us; 44us; 1us; 65535us; 42us; 45us; 1us; 65535us; 42us; 46us; 5us; 65535us; 15us; 16us; 18us; 19us; 48us; 49us; 51us; 52us; 59us; 61us; 1us; 65535us; 27us; 28us; 1us; 65535us; 58us; 59us; 2us; 65535us; 36us; 37us; 54us; 55us; 2us; 65535us; 37us; 62us; 55us; 62us; 5us; 65535us; 64us; 65us; 75us; 71us; 77us; 72us; 80us; 73us; 83us; 74us; |]
let _fsyacc_sparseGotoTableRowOffsets = [|0us; 1us; 3us; 5us; 7us; 9us; 11us; 13us; 15us; 17us; 19us; 21us; 23us; 25us; 27us; 29us; 31us; 33us; 35us; 41us; 43us; 45us; 48us; 51us; |]
let _fsyacc_stateToProdIdxsTableElements = [| 1us; 0us; 1us; 0us; 5us; 1us; 3us; 4us; 5us; 6us; 1us; 1us; 1us; 3us; 1us; 4us; 1us; 5us; 1us; 6us; 1us; 7us; 1us; 7us; 3us; 7us; 9us; 10us; 1us; 7us; 1us; 9us; 1us; 10us; 1us; 11us; 1us; 11us; 1us; 11us; 1us; 12us; 1us; 12us; 1us; 12us; 1us; 13us; 1us; 13us; 1us; 13us; 2us; 13us; 15us; 1us; 13us; 1us; 15us; 1us; 16us; 1us; 16us; 1us; 16us; 1us; 17us; 1us; 17us; 1us; 17us; 2us; 17us; 19us; 1us; 17us; 1us; 19us; 1us; 20us; 1us; 20us; 2us; 20us; 34us; 1us; 20us; 1us; 21us; 1us; 21us; 1us; 21us; 4us; 21us; 23us; 24us; 25us; 1us; 21us; 1us; 23us; 1us; 24us; 1us; 25us; 1us; 26us; 1us; 26us; 1us; 26us; 1us; 27us; 1us; 27us; 1us; 27us; 1us; 28us; 1us; 28us; 2us; 28us; 34us; 1us; 28us; 1us; 29us; 1us; 30us; 2us; 30us; 32us; 1us; 30us; 1us; 32us; 1us; 34us; 1us; 35us; 1us; 35us; 2us; 35us; 41us; 1us; 36us; 1us; 37us; 1us; 38us; 1us; 39us; 1us; 40us; 2us; 41us; 41us; 2us; 41us; 42us; 2us; 41us; 43us; 2us; 41us; 44us; 1us; 41us; 1us; 42us; 1us; 42us; 1us; 42us; 1us; 43us; 1us; 43us; 1us; 43us; 1us; 44us; 1us; 44us; 1us; 44us; |]
let _fsyacc_stateToProdIdxsTableRowOffsets = [|0us; 2us; 4us; 10us; 12us; 14us; 16us; 18us; 20us; 22us; 24us; 28us; 30us; 32us; 34us; 36us; 38us; 40us; 42us; 44us; 46us; 48us; 50us; 52us; 55us; 57us; 59us; 61us; 63us; 65us; 67us; 69us; 71us; 74us; 76us; 78us; 80us; 82us; 85us; 87us; 89us; 91us; 93us; 98us; 100us; 102us; 104us; 106us; 108us; 110us; 112us; 114us; 116us; 118us; 120us; 122us; 125us; 127us; 129us; 131us; 134us; 136us; 138us; 140us; 142us; 144us; 147us; 149us; 151us; 153us; 155us; 157us; 160us; 163us; 166us; 169us; 171us; 173us; 175us; 177us; 179us; 181us; 183us; 185us; 187us; |]
let _fsyacc_action_rows = 85
let _fsyacc_actionTableElements = [|0us; 16386us; 0us; 49152us; 5us; 32768us; 7us; 8us; 8us; 20us; 9us; 29us; 10us; 39us; 11us; 3us; 0us; 16385us; 0us; 16387us; 0us; 16388us; 0us; 16389us; 0us; 16390us; 1us; 32768us; 22us; 9us; 0us; 16392us; 3us; 32768us; 0us; 14us; 1us; 17us; 23us; 11us; 0us; 16391us; 0us; 16393us; 0us; 16394us; 1us; 32768us; 17us; 15us; 1us; 32768us; 26us; 57us; 0us; 16395us; 1us; 32768us; 17us; 18us; 1us; 32768us; 26us; 57us; 0us; 16396us; 1us; 32768us; 25us; 21us; 1us; 32768us; 22us; 22us; 0us; 16398us; 2us; 32768us; 2us; 26us; 23us; 24us; 0us; 16397us; 0us; 16399us; 1us; 32768us; 17us; 27us; 1us; 32768us; 20us; 58us; 0us; 16400us; 1us; 32768us; 25us; 30us; 1us; 32768us; 22us; 31us; 0us; 16402us; 2us; 32768us; 3us; 35us; 23us; 33us; 0us; 16401us; 0us; 16403us; 1us; 32768us; 22us; 36us; 0us; 16417us; 2us; 32768us; 23us; 38us; 25us; 63us; 0us; 16404us; 1us; 32768us; 25us; 40us; 1us; 32768us; 22us; 41us; 0us; 16406us; 4us; 32768us; 4us; 47us; 5us; 53us; 6us; 50us; 23us; 43us; 0us; 16405us; 0us; 16407us; 0us; 16408us; 0us; 16409us; 1us; 32768us; 17us; 48us; 1us; 32768us; 26us; 57us; 0us; 16410us; 1us; 32768us; 17us; 51us; 1us; 32768us; 26us; 57us; 0us; 16411us; 1us; 32768us; 22us; 54us; 0us; 16417us; 2us; 32768us; 23us; 56us; 25us; 63us; 0us; 16412us; 0us; 16413us; 0us; 16415us; 2us; 32768us; 21us; 60us; 26us; 57us; 0us; 16414us; 0us; 16416us; 0us; 16418us; 1us; 32768us; 17us; 64us; 8us; 32768us; 12us; 76us; 13us; 79us; 14us; 82us; 24us; 70us; 26us; 69us; 27us; 66us; 28us; 67us; 29us; 68us; 1us; 16419us; 15us; 75us; 0us; 16420us; 0us; 16421us; 0us; 16422us; 0us; 16423us; 0us; 16424us; 0us; 16425us; 2us; 32768us; 15us; 75us; 19us; 78us; 2us; 32768us; 15us; 75us; 19us; 81us; 2us; 32768us; 15us; 75us; 19us; 84us; 8us; 32768us; 12us; 76us; 13us; 79us; 14us; 82us; 24us; 70us; 26us; 69us; 27us; 66us; 28us; 67us; 29us; 68us; 1us; 32768us; 18us; 77us; 8us; 32768us; 12us; 76us; 13us; 79us; 14us; 82us; 24us; 70us; 26us; 69us; 27us; 66us; 28us; 67us; 29us; 68us; 0us; 16426us; 1us; 32768us; 18us; 80us; 8us; 32768us; 12us; 76us; 13us; 79us; 14us; 82us; 24us; 70us; 26us; 69us; 27us; 66us; 28us; 67us; 29us; 68us; 0us; 16427us; 1us; 32768us; 18us; 83us; 8us; 32768us; 12us; 76us; 13us; 79us; 14us; 82us; 24us; 70us; 26us; 69us; 27us; 66us; 28us; 67us; 29us; 68us; 0us; 16428us; |]
let _fsyacc_actionTableRowOffsets = [|0us; 1us; 2us; 8us; 9us; 10us; 11us; 12us; 13us; 15us; 16us; 20us; 21us; 22us; 23us; 25us; 27us; 28us; 30us; 32us; 33us; 35us; 37us; 38us; 41us; 42us; 43us; 45us; 47us; 48us; 50us; 52us; 53us; 56us; 57us; 58us; 60us; 61us; 64us; 65us; 67us; 69us; 70us; 75us; 76us; 77us; 78us; 79us; 81us; 83us; 84us; 86us; 88us; 89us; 91us; 92us; 95us; 96us; 97us; 98us; 101us; 102us; 103us; 104us; 106us; 115us; 117us; 118us; 119us; 120us; 121us; 122us; 123us; 126us; 129us; 132us; 141us; 143us; 152us; 153us; 155us; 164us; 165us; 167us; 176us; |]
let _fsyacc_reductionSymbolCounts = [|1us; 2us; 0us; 2us; 2us; 2us; 2us; 4us; 0us; 2us; 2us; 3us; 3us; 5us; 0us; 2us; 3us; 5us; 0us; 2us; 4us; 5us; 0us; 2us; 2us; 2us; 3us; 3us; 4us; 1us; 3us; 0us; 2us; 0us; 2us; 3us; 1us; 1us; 1us; 1us; 1us; 3us; 4us; 4us; 4us; |]
let _fsyacc_productionToNonTerminalTable = [|0us; 1us; 2us; 2us; 2us; 2us; 2us; 3us; 4us; 4us; 4us; 5us; 6us; 7us; 8us; 8us; 9us; 10us; 11us; 11us; 12us; 13us; 14us; 14us; 14us; 14us; 15us; 16us; 17us; 18us; 19us; 20us; 20us; 21us; 21us; 22us; 23us; 23us; 23us; 23us; 23us; 23us; 23us; 23us; 23us; |]
let _fsyacc_immediateActions = [|65535us; 49152us; 65535us; 16385us; 16387us; 16388us; 16389us; 16390us; 65535us; 65535us; 65535us; 16391us; 16393us; 16394us; 65535us; 65535us; 16395us; 65535us; 65535us; 16396us; 65535us; 65535us; 65535us; 65535us; 16397us; 16399us; 65535us; 65535us; 16400us; 65535us; 65535us; 65535us; 65535us; 16401us; 16403us; 65535us; 65535us; 65535us; 16404us; 65535us; 65535us; 65535us; 65535us; 16405us; 16407us; 16408us; 16409us; 65535us; 65535us; 16410us; 65535us; 65535us; 16411us; 65535us; 65535us; 65535us; 16412us; 16413us; 65535us; 65535us; 16414us; 16416us; 16418us; 65535us; 65535us; 65535us; 16420us; 16421us; 16422us; 16423us; 16424us; 65535us; 65535us; 65535us; 65535us; 65535us; 65535us; 65535us; 16426us; 65535us; 65535us; 16427us; 65535us; 65535us; 16428us; |]
let _fsyacc_reductions ()  =    [| 
# 315 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> Terrabuild.Parser.Workspace.AST.Workspace in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
                      raise (FSharp.Text.Parsing.Accept(Microsoft.FSharp.Core.Operators.box _1))
                   )
                 : 'gentype__startWorkspace));
# 324 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_WorkspaceComponents in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 39 "Workspace/Parser.fsy"
                                                     _1 
                   )
# 39 "Workspace/Parser.fsy"
                 : Terrabuild.Parser.Workspace.AST.Workspace));
# 335 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 42 "Workspace/Parser.fsy"
                                         Workspace.Empty 
                   )
# 42 "Workspace/Parser.fsy"
                 : 'gentype_WorkspaceComponents));
# 345 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_WorkspaceComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_Terrabuild in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 43 "Workspace/Parser.fsy"
                                                            _1.Patch _2 
                   )
# 43 "Workspace/Parser.fsy"
                 : 'gentype_WorkspaceComponents));
# 357 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_WorkspaceComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_Target in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 44 "Workspace/Parser.fsy"
                                                        _1.Patch _2 
                   )
# 44 "Workspace/Parser.fsy"
                 : 'gentype_WorkspaceComponents));
# 369 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_WorkspaceComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_Environment in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 45 "Workspace/Parser.fsy"
                                                             _1.Patch _2 
                   )
# 45 "Workspace/Parser.fsy"
                 : 'gentype_WorkspaceComponents));
# 381 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_WorkspaceComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_Extension in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 46 "Workspace/Parser.fsy"
                                                           _1.Patch _2 
                   )
# 46 "Workspace/Parser.fsy"
                 : 'gentype_WorkspaceComponents));
# 393 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_TerrabuildComponents in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 49 "Workspace/Parser.fsy"
                                                                           WorkspaceComponents.Terrabuild _3 
                   )
# 49 "Workspace/Parser.fsy"
                 : 'gentype_Terrabuild));
# 404 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 51 "Workspace/Parser.fsy"
                                         Terrabuild.Empty 
                   )
# 51 "Workspace/Parser.fsy"
                 : 'gentype_TerrabuildComponents));
# 414 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_TerrabuildComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_TerrabuildStorage in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 52 "Workspace/Parser.fsy"
                                                                    _1.Patch _2 
                   )
# 52 "Workspace/Parser.fsy"
                 : 'gentype_TerrabuildComponents));
# 426 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_TerrabuildComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_TerrabuildSourceControl in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 53 "Workspace/Parser.fsy"
                                                                          _1.Patch _2 
                   )
# 53 "Workspace/Parser.fsy"
                 : 'gentype_TerrabuildComponents));
# 438 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_String in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 55 "Workspace/Parser.fsy"
                                                  TerrabuildComponents.Storage _3 
                   )
# 55 "Workspace/Parser.fsy"
                 : 'gentype_TerrabuildStorage));
# 449 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_String in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 57 "Workspace/Parser.fsy"
                                                        TerrabuildComponents.SourceControl _3 
                   )
# 57 "Workspace/Parser.fsy"
                 : 'gentype_TerrabuildSourceControl));
# 460 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _2 = parseState.GetInput(2) :?> string in
            let _4 = parseState.GetInput(4) :?> 'gentype_TargetComponents in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 60 "Workspace/Parser.fsy"
                                                                              WorkspaceComponents.Target (_2, _4) 
                   )
# 60 "Workspace/Parser.fsy"
                 : 'gentype_Target));
# 472 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 62 "Workspace/Parser.fsy"
                                         Target.Empty 
                   )
# 62 "Workspace/Parser.fsy"
                 : 'gentype_TargetComponents));
# 482 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_TargetComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_TargetDependsOn in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 63 "Workspace/Parser.fsy"
                                                              _1.Patch _2 
                   )
# 63 "Workspace/Parser.fsy"
                 : 'gentype_TargetComponents));
# 494 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_ListOfString in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 65 "Workspace/Parser.fsy"
                                                           TargetComponents.DependsOn _3 
                   )
# 65 "Workspace/Parser.fsy"
                 : 'gentype_TargetDependsOn));
# 505 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _2 = parseState.GetInput(2) :?> string in
            let _4 = parseState.GetInput(4) :?> 'gentype_EnvironmentComponents in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 68 "Workspace/Parser.fsy"
                                                                                        WorkspaceComponents.Environment (_2, _4) 
                   )
# 68 "Workspace/Parser.fsy"
                 : 'gentype_Environment));
# 517 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 70 "Workspace/Parser.fsy"
                                         Environment.Empty 
                   )
# 70 "Workspace/Parser.fsy"
                 : 'gentype_EnvironmentComponents));
# 527 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_EnvironmentComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_EnvironmentVariables in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 71 "Workspace/Parser.fsy"
                                                                        _1.Patch _2 
                   )
# 71 "Workspace/Parser.fsy"
                 : 'gentype_EnvironmentComponents));
# 539 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_Variables in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 73 "Workspace/Parser.fsy"
                                                               EnvironmentComponents.Variables _3 
                   )
# 73 "Workspace/Parser.fsy"
                 : 'gentype_EnvironmentVariables));
# 550 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _2 = parseState.GetInput(2) :?> string in
            let _4 = parseState.GetInput(4) :?> 'gentype_ExtensionComponents in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 76 "Workspace/Parser.fsy"
                                                                                    WorkspaceComponents.Extension (_2, _4) 
                   )
# 76 "Workspace/Parser.fsy"
                 : 'gentype_Extension));
# 562 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 78 "Workspace/Parser.fsy"
                                         Extension.Empty 
                   )
# 78 "Workspace/Parser.fsy"
                 : 'gentype_ExtensionComponents));
# 572 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_ExtensionComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_Container in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 79 "Workspace/Parser.fsy"
                                                           _1.Patch _2 
                   )
# 79 "Workspace/Parser.fsy"
                 : 'gentype_ExtensionComponents));
# 584 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_ExtensionComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_Script in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 80 "Workspace/Parser.fsy"
                                                        _1.Patch _2 
                   )
# 80 "Workspace/Parser.fsy"
                 : 'gentype_ExtensionComponents));
# 596 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_ExtensionComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_Parameters in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 81 "Workspace/Parser.fsy"
                                                            _1.Patch _2 
                   )
# 81 "Workspace/Parser.fsy"
                 : 'gentype_ExtensionComponents));
# 608 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_String in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 83 "Workspace/Parser.fsy"
                                                    ExtensionComponents.Container _3 
                   )
# 83 "Workspace/Parser.fsy"
                 : 'gentype_Container));
# 619 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_String in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 85 "Workspace/Parser.fsy"
                                                 ExtensionComponents.Script _3 
                   )
# 85 "Workspace/Parser.fsy"
                 : 'gentype_Script));
# 630 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_Variables in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 87 "Workspace/Parser.fsy"
                                                                ExtensionComponents.Parameters _3 
                   )
# 87 "Workspace/Parser.fsy"
                 : 'gentype_Parameters));
# 641 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 91 "Workspace/Parser.fsy"
                                    _1 
                   )
# 91 "Workspace/Parser.fsy"
                 : 'gentype_String));
# 652 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _2 = parseState.GetInput(2) :?> 'gentype_Strings in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 93 "Workspace/Parser.fsy"
                                                           _2 
                   )
# 93 "Workspace/Parser.fsy"
                 : 'gentype_ListOfString));
# 663 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 95 "Workspace/Parser.fsy"
                                         [] 
                   )
# 95 "Workspace/Parser.fsy"
                 : 'gentype_Strings));
# 673 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_Strings in
            let _2 = parseState.GetInput(2) :?> 'gentype_String in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 96 "Workspace/Parser.fsy"
                                            _1 @ [_2] 
                   )
# 96 "Workspace/Parser.fsy"
                 : 'gentype_Strings));
# 685 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 99 "Workspace/Parser.fsy"
                                         Map.empty 
                   )
# 99 "Workspace/Parser.fsy"
                 : 'gentype_Variables));
# 695 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_Variables in
            let _2 = parseState.GetInput(2) :?> 'gentype_Variable in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 100 "Workspace/Parser.fsy"
                                                _1.Add _2 
                   )
# 100 "Workspace/Parser.fsy"
                 : 'gentype_Variables));
# 707 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            let _3 = parseState.GetInput(3) :?> 'gentype_Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 102 "Workspace/Parser.fsy"
                                                   (_1, _3) 
                   )
# 102 "Workspace/Parser.fsy"
                 : 'gentype_Variable));
# 719 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 105 "Workspace/Parser.fsy"
                                     Nothing 
                   )
# 105 "Workspace/Parser.fsy"
                 : 'gentype_Expr));
# 729 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 106 "Workspace/Parser.fsy"
                                  Boolean true 
                   )
# 106 "Workspace/Parser.fsy"
                 : 'gentype_Expr));
# 739 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 107 "Workspace/Parser.fsy"
                                   Boolean false 
                   )
# 107 "Workspace/Parser.fsy"
                 : 'gentype_Expr));
# 749 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 108 "Workspace/Parser.fsy"
                                    String _1 
                   )
# 108 "Workspace/Parser.fsy"
                 : 'gentype_Expr));
# 760 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 109 "Workspace/Parser.fsy"
                                      Variable _1 
                   )
# 109 "Workspace/Parser.fsy"
                 : 'gentype_Expr));
# 771 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_Expr in
            let _3 = parseState.GetInput(3) :?> 'gentype_Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 110 "Workspace/Parser.fsy"
                                            InfixFunction (_1, Plus, _3) 
                   )
# 110 "Workspace/Parser.fsy"
                 : 'gentype_Expr));
# 783 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 111 "Workspace/Parser.fsy"
                                                     Function (Trim, _3) 
                   )
# 111 "Workspace/Parser.fsy"
                 : 'gentype_Expr));
# 794 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 112 "Workspace/Parser.fsy"
                                                      Function (Upper, _3) 
                   )
# 112 "Workspace/Parser.fsy"
                 : 'gentype_Expr));
# 805 "Workspace/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 113 "Workspace/Parser.fsy"
                                                      Function (Lower, _3) 
                   )
# 113 "Workspace/Parser.fsy"
                 : 'gentype_Expr));
|]
# 817 "Workspace/Parser.fs"
let tables : FSharp.Text.Parsing.Tables<_> = 
  { reductions= _fsyacc_reductions ();
    endOfInputTag = _fsyacc_endOfInputTag;
    tagOfToken = tagOfToken;
    dataOfToken = _fsyacc_dataOfToken; 
    actionTableElements = _fsyacc_actionTableElements;
    actionTableRowOffsets = _fsyacc_actionTableRowOffsets;
    stateToProdIdxsTableElements = _fsyacc_stateToProdIdxsTableElements;
    stateToProdIdxsTableRowOffsets = _fsyacc_stateToProdIdxsTableRowOffsets;
    reductionSymbolCounts = _fsyacc_reductionSymbolCounts;
    immediateActions = _fsyacc_immediateActions;
    gotos = _fsyacc_gotos;
    sparseGotoTableRowOffsets = _fsyacc_sparseGotoTableRowOffsets;
    tagOfErrorTerminal = _fsyacc_tagOfErrorTerminal;
    parseError = (fun (ctxt:FSharp.Text.Parsing.ParseErrorContext<_>) -> 
                              match parse_error_rich with 
                              | Some f -> f ctxt
                              | None -> parse_error ctxt.Message);
    numTerminals = 33;
    productionToNonTerminalTable = _fsyacc_productionToNonTerminalTable  }
let engine lexer lexbuf startState = tables.Interpret(lexer, lexbuf, startState)
let Workspace lexer lexbuf : Terrabuild.Parser.Workspace.AST.Workspace =
    engine lexer lexbuf 0 :?> _
