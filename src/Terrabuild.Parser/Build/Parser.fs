// Implementation file for parser generated by fsyacc
module BuildParser
#nowarn "64";; // turn off warnings that type variables used in production annotations are instantiated to concrete type
open FSharp.Text.Lexing
open FSharp.Text.Parsing.ParseHelpers
# 1 "Build/Parser.fsy"
 
open Terrabuild.Parser.AST
open Terrabuild.Parser.Build.AST


#if DEBUG
let debugPrint s = printfn "### %s" s
#else
let debugPrint s = ignore s
#endif


# 19 "Build/Parser.fs"
// This type is the type of tokens accepted by the parser
type token = 
  | DEPENDENCIES
  | OUTPUTS
  | LABELS
  | PARSER
  | CONTAINER
  | PARAMETERS
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
  | VARIABLE of (string)
  | IDENTIFIER of (string)
  | STRING of (string)
  | NOTHING
  | TRUE
  | FALSE
// This type is used to give symbolic names to token indexes, useful for error messages
type tokenId = 
    | TOKEN_DEPENDENCIES
    | TOKEN_OUTPUTS
    | TOKEN_LABELS
    | TOKEN_PARSER
    | TOKEN_CONTAINER
    | TOKEN_PARAMETERS
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
    | NONTERM__startBuild
    | NONTERM_Build
    | NONTERM_BuildComponents
    | NONTERM_Extension
    | NONTERM_ExtensionComponents
    | NONTERM_Container
    | NONTERM_Parameters
    | NONTERM_Project
    | NONTERM_ProjectComponents
    | NONTERM_ProjectDependencies
    | NONTERM_ProjectOutputs
    | NONTERM_ProjectLabels
    | NONTERM_ProjectParser
    | NONTERM_Target
    | NONTERM_TargetComponents
    | NONTERM_Command
    | NONTERM_String
    | NONTERM_ListOfString
    | NONTERM_Strings
    | NONTERM_Variables
    | NONTERM_Variable
    | NONTERM_Expr

// This function maps tokens to integer indexes
let tagOfToken (t:token) = 
  match t with
  | DEPENDENCIES  -> 0 
  | OUTPUTS  -> 1 
  | LABELS  -> 2 
  | PARSER  -> 3 
  | CONTAINER  -> 4 
  | PARAMETERS  -> 5 
  | EXTENSION  -> 6 
  | PROJECT  -> 7 
  | TARGET  -> 8 
  | EOF  -> 9 
  | TRIM  -> 10 
  | UPPER  -> 11 
  | LOWER  -> 12 
  | PLUS  -> 13 
  | COMMA  -> 14 
  | EQUAL  -> 15 
  | LPAREN  -> 16 
  | RPAREN  -> 17 
  | LSQBRACKET  -> 18 
  | RSQBRACKET  -> 19 
  | LBRACE  -> 20 
  | RBRACE  -> 21 
  | VARIABLE _ -> 22 
  | IDENTIFIER _ -> 23 
  | STRING _ -> 24 
  | NOTHING  -> 25 
  | TRUE  -> 26 
  | FALSE  -> 27 

// This function maps integer indexes to symbolic token ids
let tokenTagToTokenId (tokenIdx:int) = 
  match tokenIdx with
  | 0 -> TOKEN_DEPENDENCIES 
  | 1 -> TOKEN_OUTPUTS 
  | 2 -> TOKEN_LABELS 
  | 3 -> TOKEN_PARSER 
  | 4 -> TOKEN_CONTAINER 
  | 5 -> TOKEN_PARAMETERS 
  | 6 -> TOKEN_EXTENSION 
  | 7 -> TOKEN_PROJECT 
  | 8 -> TOKEN_TARGET 
  | 9 -> TOKEN_EOF 
  | 10 -> TOKEN_TRIM 
  | 11 -> TOKEN_UPPER 
  | 12 -> TOKEN_LOWER 
  | 13 -> TOKEN_PLUS 
  | 14 -> TOKEN_COMMA 
  | 15 -> TOKEN_EQUAL 
  | 16 -> TOKEN_LPAREN 
  | 17 -> TOKEN_RPAREN 
  | 18 -> TOKEN_LSQBRACKET 
  | 19 -> TOKEN_RSQBRACKET 
  | 20 -> TOKEN_LBRACE 
  | 21 -> TOKEN_RBRACE 
  | 22 -> TOKEN_VARIABLE 
  | 23 -> TOKEN_IDENTIFIER 
  | 24 -> TOKEN_STRING 
  | 25 -> TOKEN_NOTHING 
  | 26 -> TOKEN_TRUE 
  | 27 -> TOKEN_FALSE 
  | 30 -> TOKEN_end_of_input
  | 28 -> TOKEN_error
  | _ -> failwith "tokenTagToTokenId: bad token"

/// This function maps production indexes returned in syntax errors to strings representing the non terminal that would be produced by that production
let prodIdxToNonTerminal (prodIdx:int) = 
  match prodIdx with
    | 0 -> NONTERM__startBuild 
    | 1 -> NONTERM_Build 
    | 2 -> NONTERM_BuildComponents 
    | 3 -> NONTERM_BuildComponents 
    | 4 -> NONTERM_BuildComponents 
    | 5 -> NONTERM_BuildComponents 
    | 6 -> NONTERM_Extension 
    | 7 -> NONTERM_ExtensionComponents 
    | 8 -> NONTERM_ExtensionComponents 
    | 9 -> NONTERM_ExtensionComponents 
    | 10 -> NONTERM_Container 
    | 11 -> NONTERM_Parameters 
    | 12 -> NONTERM_Project 
    | 13 -> NONTERM_ProjectComponents 
    | 14 -> NONTERM_ProjectComponents 
    | 15 -> NONTERM_ProjectComponents 
    | 16 -> NONTERM_ProjectComponents 
    | 17 -> NONTERM_ProjectComponents 
    | 18 -> NONTERM_ProjectDependencies 
    | 19 -> NONTERM_ProjectOutputs 
    | 20 -> NONTERM_ProjectLabels 
    | 21 -> NONTERM_ProjectParser 
    | 22 -> NONTERM_Target 
    | 23 -> NONTERM_TargetComponents 
    | 24 -> NONTERM_TargetComponents 
    | 25 -> NONTERM_Command 
    | 26 -> NONTERM_String 
    | 27 -> NONTERM_ListOfString 
    | 28 -> NONTERM_Strings 
    | 29 -> NONTERM_Strings 
    | 30 -> NONTERM_Variables 
    | 31 -> NONTERM_Variables 
    | 32 -> NONTERM_Variable 
    | 33 -> NONTERM_Expr 
    | 34 -> NONTERM_Expr 
    | 35 -> NONTERM_Expr 
    | 36 -> NONTERM_Expr 
    | 37 -> NONTERM_Expr 
    | 38 -> NONTERM_Expr 
    | 39 -> NONTERM_Expr 
    | 40 -> NONTERM_Expr 
    | 41 -> NONTERM_Expr 
    | _ -> failwith "prodIdxToNonTerminal: bad production index"

let _fsyacc_endOfInputTag = 30 
let _fsyacc_tagOfErrorTerminal = 28

// This function gets the name of a token as a string
let token_to_string (t:token) = 
  match t with 
  | DEPENDENCIES  -> "DEPENDENCIES" 
  | OUTPUTS  -> "OUTPUTS" 
  | LABELS  -> "LABELS" 
  | PARSER  -> "PARSER" 
  | CONTAINER  -> "CONTAINER" 
  | PARAMETERS  -> "PARAMETERS" 
  | EXTENSION  -> "EXTENSION" 
  | PROJECT  -> "PROJECT" 
  | TARGET  -> "TARGET" 
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
  | DEPENDENCIES  -> (null : System.Object) 
  | OUTPUTS  -> (null : System.Object) 
  | LABELS  -> (null : System.Object) 
  | PARSER  -> (null : System.Object) 
  | CONTAINER  -> (null : System.Object) 
  | PARAMETERS  -> (null : System.Object) 
  | EXTENSION  -> (null : System.Object) 
  | PROJECT  -> (null : System.Object) 
  | TARGET  -> (null : System.Object) 
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
let _fsyacc_gotos = [| 0us; 65535us; 1us; 65535us; 0us; 1us; 1us; 65535us; 0us; 2us; 1us; 65535us; 2us; 4us; 1us; 65535us; 9us; 10us; 1us; 65535us; 10us; 12us; 1us; 65535us; 10us; 13us; 1us; 65535us; 2us; 5us; 1us; 65535us; 22us; 23us; 1us; 65535us; 23us; 25us; 1us; 65535us; 23us; 26us; 1us; 65535us; 23us; 27us; 1us; 65535us; 23us; 28us; 1us; 65535us; 2us; 6us; 1us; 65535us; 43us; 44us; 1us; 65535us; 44us; 46us; 3us; 65535us; 15us; 16us; 39us; 40us; 54us; 56us; 3us; 65535us; 30us; 31us; 33us; 34us; 36us; 37us; 1us; 65535us; 53us; 54us; 2us; 65535us; 18us; 19us; 49us; 50us; 2us; 65535us; 19us; 57us; 50us; 57us; 5us; 65535us; 59us; 60us; 70us; 66us; 72us; 67us; 75us; 68us; 78us; 69us; |]
let _fsyacc_sparseGotoTableRowOffsets = [|0us; 1us; 3us; 5us; 7us; 9us; 11us; 13us; 15us; 17us; 19us; 21us; 23us; 25us; 27us; 29us; 31us; 35us; 39us; 41us; 44us; 47us; |]
let _fsyacc_stateToProdIdxsTableElements = [| 1us; 0us; 1us; 0us; 4us; 1us; 3us; 4us; 5us; 1us; 1us; 1us; 3us; 1us; 4us; 1us; 5us; 1us; 6us; 1us; 6us; 1us; 6us; 3us; 6us; 8us; 9us; 1us; 6us; 1us; 8us; 1us; 9us; 1us; 10us; 1us; 10us; 1us; 10us; 1us; 11us; 1us; 11us; 2us; 11us; 31us; 1us; 11us; 1us; 12us; 1us; 12us; 5us; 12us; 14us; 15us; 16us; 17us; 1us; 12us; 1us; 14us; 1us; 15us; 1us; 16us; 1us; 17us; 1us; 18us; 1us; 18us; 1us; 18us; 1us; 19us; 1us; 19us; 1us; 19us; 1us; 20us; 1us; 20us; 1us; 20us; 1us; 21us; 1us; 21us; 1us; 21us; 1us; 22us; 1us; 22us; 1us; 22us; 2us; 22us; 24us; 1us; 22us; 1us; 24us; 1us; 25us; 1us; 25us; 1us; 25us; 2us; 25us; 31us; 1us; 25us; 1us; 26us; 1us; 27us; 2us; 27us; 29us; 1us; 27us; 1us; 29us; 1us; 31us; 1us; 32us; 1us; 32us; 2us; 32us; 38us; 1us; 33us; 1us; 34us; 1us; 35us; 1us; 36us; 1us; 37us; 2us; 38us; 38us; 2us; 38us; 39us; 2us; 38us; 40us; 2us; 38us; 41us; 1us; 38us; 1us; 39us; 1us; 39us; 1us; 39us; 1us; 40us; 1us; 40us; 1us; 40us; 1us; 41us; 1us; 41us; 1us; 41us; |]
let _fsyacc_stateToProdIdxsTableRowOffsets = [|0us; 2us; 4us; 9us; 11us; 13us; 15us; 17us; 19us; 21us; 23us; 27us; 29us; 31us; 33us; 35us; 37us; 39us; 41us; 43us; 46us; 48us; 50us; 52us; 58us; 60us; 62us; 64us; 66us; 68us; 70us; 72us; 74us; 76us; 78us; 80us; 82us; 84us; 86us; 88us; 90us; 92us; 94us; 96us; 98us; 101us; 103us; 105us; 107us; 109us; 111us; 114us; 116us; 118us; 120us; 123us; 125us; 127us; 129us; 131us; 133us; 136us; 138us; 140us; 142us; 144us; 146us; 149us; 152us; 155us; 158us; 160us; 162us; 164us; 166us; 168us; 170us; 172us; 174us; 176us; |]
let _fsyacc_action_rows = 80
let _fsyacc_actionTableElements = [|0us; 16386us; 0us; 49152us; 4us; 32768us; 6us; 7us; 7us; 21us; 8us; 41us; 9us; 3us; 0us; 16385us; 0us; 16387us; 0us; 16388us; 0us; 16389us; 1us; 32768us; 23us; 8us; 1us; 32768us; 20us; 9us; 0us; 16391us; 3us; 32768us; 4us; 14us; 5us; 17us; 21us; 11us; 0us; 16390us; 0us; 16392us; 0us; 16393us; 1us; 32768us; 15us; 15us; 1us; 32768us; 24us; 52us; 0us; 16394us; 1us; 32768us; 20us; 18us; 0us; 16414us; 2us; 32768us; 21us; 20us; 23us; 58us; 0us; 16395us; 1us; 32768us; 20us; 22us; 0us; 16397us; 5us; 32768us; 0us; 29us; 1us; 32us; 2us; 35us; 3us; 38us; 21us; 24us; 0us; 16396us; 0us; 16398us; 0us; 16399us; 0us; 16400us; 0us; 16401us; 1us; 32768us; 15us; 30us; 1us; 32768us; 18us; 53us; 0us; 16402us; 1us; 32768us; 15us; 33us; 1us; 32768us; 18us; 53us; 0us; 16403us; 1us; 32768us; 15us; 36us; 1us; 32768us; 18us; 53us; 0us; 16404us; 1us; 32768us; 15us; 39us; 1us; 32768us; 24us; 52us; 0us; 16405us; 1us; 32768us; 23us; 42us; 1us; 32768us; 20us; 43us; 0us; 16407us; 2us; 32768us; 21us; 45us; 23us; 47us; 0us; 16406us; 0us; 16408us; 1us; 32768us; 23us; 48us; 1us; 32768us; 20us; 49us; 0us; 16414us; 2us; 32768us; 21us; 51us; 23us; 58us; 0us; 16409us; 0us; 16410us; 0us; 16412us; 2us; 32768us; 19us; 55us; 24us; 52us; 0us; 16411us; 0us; 16413us; 0us; 16415us; 1us; 32768us; 15us; 59us; 8us; 32768us; 10us; 71us; 11us; 74us; 12us; 77us; 22us; 65us; 24us; 64us; 25us; 61us; 26us; 62us; 27us; 63us; 1us; 16416us; 13us; 70us; 0us; 16417us; 0us; 16418us; 0us; 16419us; 0us; 16420us; 0us; 16421us; 0us; 16422us; 2us; 32768us; 13us; 70us; 17us; 73us; 2us; 32768us; 13us; 70us; 17us; 76us; 2us; 32768us; 13us; 70us; 17us; 79us; 8us; 32768us; 10us; 71us; 11us; 74us; 12us; 77us; 22us; 65us; 24us; 64us; 25us; 61us; 26us; 62us; 27us; 63us; 1us; 32768us; 16us; 72us; 8us; 32768us; 10us; 71us; 11us; 74us; 12us; 77us; 22us; 65us; 24us; 64us; 25us; 61us; 26us; 62us; 27us; 63us; 0us; 16423us; 1us; 32768us; 16us; 75us; 8us; 32768us; 10us; 71us; 11us; 74us; 12us; 77us; 22us; 65us; 24us; 64us; 25us; 61us; 26us; 62us; 27us; 63us; 0us; 16424us; 1us; 32768us; 16us; 78us; 8us; 32768us; 10us; 71us; 11us; 74us; 12us; 77us; 22us; 65us; 24us; 64us; 25us; 61us; 26us; 62us; 27us; 63us; 0us; 16425us; |]
let _fsyacc_actionTableRowOffsets = [|0us; 1us; 2us; 7us; 8us; 9us; 10us; 11us; 13us; 15us; 16us; 20us; 21us; 22us; 23us; 25us; 27us; 28us; 30us; 31us; 34us; 35us; 37us; 38us; 44us; 45us; 46us; 47us; 48us; 49us; 51us; 53us; 54us; 56us; 58us; 59us; 61us; 63us; 64us; 66us; 68us; 69us; 71us; 73us; 74us; 77us; 78us; 79us; 81us; 83us; 84us; 87us; 88us; 89us; 90us; 93us; 94us; 95us; 96us; 98us; 107us; 109us; 110us; 111us; 112us; 113us; 114us; 115us; 118us; 121us; 124us; 133us; 135us; 144us; 145us; 147us; 156us; 157us; 159us; 168us; |]
let _fsyacc_reductionSymbolCounts = [|1us; 2us; 0us; 2us; 2us; 2us; 5us; 0us; 2us; 2us; 3us; 4us; 4us; 0us; 2us; 2us; 2us; 2us; 3us; 3us; 3us; 3us; 5us; 0us; 2us; 5us; 1us; 3us; 0us; 2us; 0us; 2us; 3us; 1us; 1us; 1us; 1us; 1us; 3us; 4us; 4us; 4us; |]
let _fsyacc_productionToNonTerminalTable = [|0us; 1us; 2us; 2us; 2us; 2us; 3us; 4us; 4us; 4us; 5us; 6us; 7us; 8us; 8us; 8us; 8us; 8us; 9us; 10us; 11us; 12us; 13us; 14us; 14us; 15us; 16us; 17us; 18us; 18us; 19us; 19us; 20us; 21us; 21us; 21us; 21us; 21us; 21us; 21us; 21us; 21us; |]
let _fsyacc_immediateActions = [|65535us; 49152us; 65535us; 16385us; 16387us; 16388us; 16389us; 65535us; 65535us; 65535us; 65535us; 16390us; 16392us; 16393us; 65535us; 65535us; 16394us; 65535us; 65535us; 65535us; 16395us; 65535us; 65535us; 65535us; 16396us; 16398us; 16399us; 16400us; 16401us; 65535us; 65535us; 16402us; 65535us; 65535us; 16403us; 65535us; 65535us; 16404us; 65535us; 65535us; 16405us; 65535us; 65535us; 65535us; 65535us; 16406us; 16408us; 65535us; 65535us; 65535us; 65535us; 16409us; 16410us; 65535us; 65535us; 16411us; 16413us; 16415us; 65535us; 65535us; 65535us; 16417us; 16418us; 16419us; 16420us; 16421us; 65535us; 65535us; 65535us; 65535us; 65535us; 65535us; 65535us; 16423us; 65535us; 65535us; 16424us; 65535us; 65535us; 16425us; |]
let _fsyacc_reductions ()  =    [| 
# 298 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> Terrabuild.Parser.Build.AST.Build in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
                      raise (FSharp.Text.Parsing.Accept(Microsoft.FSharp.Core.Operators.box _1))
                   )
                 : 'gentype__startBuild));
# 307 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_BuildComponents in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 39 "Build/Parser.fsy"
                                                 _1 
                   )
# 39 "Build/Parser.fsy"
                 : Terrabuild.Parser.Build.AST.Build));
# 318 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 42 "Build/Parser.fsy"
                                         Build.Empty 
                   )
# 42 "Build/Parser.fsy"
                 : 'gentype_BuildComponents));
# 328 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_BuildComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_Extension in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 43 "Build/Parser.fsy"
                                                       _1.Patch _2 
                   )
# 43 "Build/Parser.fsy"
                 : 'gentype_BuildComponents));
# 340 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_BuildComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_Project in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 44 "Build/Parser.fsy"
                                                     _1.Patch _2 
                   )
# 44 "Build/Parser.fsy"
                 : 'gentype_BuildComponents));
# 352 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_BuildComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_Target in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 45 "Build/Parser.fsy"
                                                    _1.Patch _2 
                   )
# 45 "Build/Parser.fsy"
                 : 'gentype_BuildComponents));
# 364 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _2 = parseState.GetInput(2) :?> string in
            let _4 = parseState.GetInput(4) :?> 'gentype_ExtensionComponents in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 49 "Build/Parser.fsy"
                                                                                    BuildComponents.Extension (_2, _4) 
                   )
# 49 "Build/Parser.fsy"
                 : 'gentype_Extension));
# 376 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 51 "Build/Parser.fsy"
                                         Extension.Empty 
                   )
# 51 "Build/Parser.fsy"
                 : 'gentype_ExtensionComponents));
# 386 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_ExtensionComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_Container in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 52 "Build/Parser.fsy"
                                                           _1.Patch _2 
                   )
# 52 "Build/Parser.fsy"
                 : 'gentype_ExtensionComponents));
# 398 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_ExtensionComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_Parameters in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 53 "Build/Parser.fsy"
                                                            _1.Patch _2 
                   )
# 53 "Build/Parser.fsy"
                 : 'gentype_ExtensionComponents));
# 410 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_String in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 55 "Build/Parser.fsy"
                                                    ExtensionComponents.Container _3 
                   )
# 55 "Build/Parser.fsy"
                 : 'gentype_Container));
# 421 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_Variables in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 57 "Build/Parser.fsy"
                                                                ExtensionComponents.Parameters _3 
                   )
# 57 "Build/Parser.fsy"
                 : 'gentype_Parameters));
# 432 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_ProjectComponents in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 61 "Build/Parser.fsy"
                                                                     BuildComponents.Project _3 
                   )
# 61 "Build/Parser.fsy"
                 : 'gentype_Project));
# 443 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 63 "Build/Parser.fsy"
                                         Project.Empty 
                   )
# 63 "Build/Parser.fsy"
                 : 'gentype_ProjectComponents));
# 453 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_ProjectComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_ProjectDependencies in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 64 "Build/Parser.fsy"
                                                                   _1.Patch _2 
                   )
# 64 "Build/Parser.fsy"
                 : 'gentype_ProjectComponents));
# 465 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_ProjectComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_ProjectOutputs in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 65 "Build/Parser.fsy"
                                                              _1.Patch _2 
                   )
# 65 "Build/Parser.fsy"
                 : 'gentype_ProjectComponents));
# 477 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_ProjectComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_ProjectLabels in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 66 "Build/Parser.fsy"
                                                             _1.Patch _2 
                   )
# 66 "Build/Parser.fsy"
                 : 'gentype_ProjectComponents));
# 489 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_ProjectComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_ProjectParser in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 67 "Build/Parser.fsy"
                                                             _1.Patch _2 
                   )
# 67 "Build/Parser.fsy"
                 : 'gentype_ProjectComponents));
# 501 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_ListOfString in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 69 "Build/Parser.fsy"
                                                             ProjectComponents.Dependencies _3 
                   )
# 69 "Build/Parser.fsy"
                 : 'gentype_ProjectDependencies));
# 512 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_ListOfString in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 71 "Build/Parser.fsy"
                                                        ProjectComponents.Outputs _3 
                   )
# 71 "Build/Parser.fsy"
                 : 'gentype_ProjectOutputs));
# 523 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_ListOfString in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 73 "Build/Parser.fsy"
                                                       ProjectComponents.Labels _3 
                   )
# 73 "Build/Parser.fsy"
                 : 'gentype_ProjectLabels));
# 534 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_String in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 75 "Build/Parser.fsy"
                                                 ProjectComponents.Parser _3 
                   )
# 75 "Build/Parser.fsy"
                 : 'gentype_ProjectParser));
# 545 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _2 = parseState.GetInput(2) :?> string in
            let _4 = parseState.GetInput(4) :?> 'gentype_TargetComponents in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 79 "Build/Parser.fsy"
                                                                              BuildComponents.Target (_2, _4) 
                   )
# 79 "Build/Parser.fsy"
                 : 'gentype_Target));
# 557 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 81 "Build/Parser.fsy"
                                         Target.Empty 
                   )
# 81 "Build/Parser.fsy"
                 : 'gentype_TargetComponents));
# 567 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_TargetComponents in
            let _2 = parseState.GetInput(2) :?> 'gentype_Command in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 82 "Build/Parser.fsy"
                                                      _1.Patch _2 
                   )
# 82 "Build/Parser.fsy"
                 : 'gentype_TargetComponents));
# 579 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            let _2 = parseState.GetInput(2) :?> string in
            let _4 = parseState.GetInput(4) :?> 'gentype_Variables in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 84 "Build/Parser.fsy"
                                                                           TargetComponents.Command { Extension = _1; Command = _2; Parameters = _4 } 
                   )
# 84 "Build/Parser.fsy"
                 : 'gentype_Command));
# 592 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 87 "Build/Parser.fsy"
                                    _1 
                   )
# 87 "Build/Parser.fsy"
                 : 'gentype_String));
# 603 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _2 = parseState.GetInput(2) :?> 'gentype_Strings in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 89 "Build/Parser.fsy"
                                                           _2 
                   )
# 89 "Build/Parser.fsy"
                 : 'gentype_ListOfString));
# 614 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 91 "Build/Parser.fsy"
                                         [] 
                   )
# 91 "Build/Parser.fsy"
                 : 'gentype_Strings));
# 624 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_Strings in
            let _2 = parseState.GetInput(2) :?> 'gentype_String in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 92 "Build/Parser.fsy"
                                            _1 @ [_2] 
                   )
# 92 "Build/Parser.fsy"
                 : 'gentype_Strings));
# 636 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 95 "Build/Parser.fsy"
                                         Map.empty 
                   )
# 95 "Build/Parser.fsy"
                 : 'gentype_Variables));
# 646 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_Variables in
            let _2 = parseState.GetInput(2) :?> 'gentype_Variable in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 96 "Build/Parser.fsy"
                                                _1.Add _2 
                   )
# 96 "Build/Parser.fsy"
                 : 'gentype_Variables));
# 658 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            let _3 = parseState.GetInput(3) :?> 'gentype_Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 98 "Build/Parser.fsy"
                                                   (_1, _3) 
                   )
# 98 "Build/Parser.fsy"
                 : 'gentype_Variable));
# 670 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 101 "Build/Parser.fsy"
                                     Nothing 
                   )
# 101 "Build/Parser.fsy"
                 : 'gentype_Expr));
# 680 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 102 "Build/Parser.fsy"
                                  Boolean true 
                   )
# 102 "Build/Parser.fsy"
                 : 'gentype_Expr));
# 690 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 103 "Build/Parser.fsy"
                                   Boolean false 
                   )
# 103 "Build/Parser.fsy"
                 : 'gentype_Expr));
# 700 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 104 "Build/Parser.fsy"
                                    String _1 
                   )
# 104 "Build/Parser.fsy"
                 : 'gentype_Expr));
# 711 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 105 "Build/Parser.fsy"
                                      Variable _1 
                   )
# 105 "Build/Parser.fsy"
                 : 'gentype_Expr));
# 722 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_Expr in
            let _3 = parseState.GetInput(3) :?> 'gentype_Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 106 "Build/Parser.fsy"
                                            InfixFunction (_1, Plus, _3) 
                   )
# 106 "Build/Parser.fsy"
                 : 'gentype_Expr));
# 734 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 107 "Build/Parser.fsy"
                                                     Function (Trim, _3) 
                   )
# 107 "Build/Parser.fsy"
                 : 'gentype_Expr));
# 745 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 108 "Build/Parser.fsy"
                                                      Function (Upper, _3) 
                   )
# 108 "Build/Parser.fsy"
                 : 'gentype_Expr));
# 756 "Build/Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> 'gentype_Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 109 "Build/Parser.fsy"
                                                      Function (Lower, _3) 
                   )
# 109 "Build/Parser.fsy"
                 : 'gentype_Expr));
|]
# 768 "Build/Parser.fs"
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
    numTerminals = 31;
    productionToNonTerminalTable = _fsyacc_productionToNonTerminalTable  }
let engine lexer lexbuf startState = tables.Interpret(lexer, lexbuf, startState)
let Build lexer lexbuf : Terrabuild.Parser.Build.AST.Build =
    engine lexer lexbuf 0 :?> _
