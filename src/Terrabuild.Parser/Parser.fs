// Implementation file for parser generated by fsyacc
module Parser
#nowarn "64";; // turn off warnings that type variables used in production annotations are instantiated to concrete type
open FSharp.Text.Lexing
open FSharp.Text.Parsing.ParseHelpers
# 1 "Parser.fsy"
 
open AST


#if DEBUG
let debugPrint s = printfn "### %s" s
#else
let debugPrint s = ignore s
#endif


# 18 "Parser.fs"
// This type is the type of tokens accepted by the parser
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
// This type is used to give symbolic names to token indexes, useful for error messages
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
// This type is used to give symbolic names to token indexes, useful for error messages
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

// This function maps tokens to integer indexes
let tagOfToken (t:token) = 
  match t with
  | EOF  -> 0 
  | TRIM  -> 1 
  | UPPER  -> 2 
  | LOWER  -> 3 
  | PLUS  -> 4 
  | COMMA  -> 5 
  | EQUAL  -> 6 
  | LPAREN  -> 7 
  | RPAREN  -> 8 
  | LSQBRACKET  -> 9 
  | RSQBRACKET  -> 10 
  | LBRACE  -> 11 
  | RBRACE  -> 12 
  | VARIABLE _ -> 13 
  | IDENTIFIER _ -> 14 
  | STRING _ -> 15 
  | NOTHING  -> 16 
  | TRUE  -> 17 
  | FALSE  -> 18 

// This function maps integer indexes to symbolic token ids
let tokenTagToTokenId (tokenIdx:int) = 
  match tokenIdx with
  | 0 -> TOKEN_EOF 
  | 1 -> TOKEN_TRIM 
  | 2 -> TOKEN_UPPER 
  | 3 -> TOKEN_LOWER 
  | 4 -> TOKEN_PLUS 
  | 5 -> TOKEN_COMMA 
  | 6 -> TOKEN_EQUAL 
  | 7 -> TOKEN_LPAREN 
  | 8 -> TOKEN_RPAREN 
  | 9 -> TOKEN_LSQBRACKET 
  | 10 -> TOKEN_RSQBRACKET 
  | 11 -> TOKEN_LBRACE 
  | 12 -> TOKEN_RBRACE 
  | 13 -> TOKEN_VARIABLE 
  | 14 -> TOKEN_IDENTIFIER 
  | 15 -> TOKEN_STRING 
  | 16 -> TOKEN_NOTHING 
  | 17 -> TOKEN_TRUE 
  | 18 -> TOKEN_FALSE 
  | 21 -> TOKEN_end_of_input
  | 19 -> TOKEN_error
  | _ -> failwith "tokenTagToTokenId: bad token"

/// This function maps production indexes returned in syntax errors to strings representing the non terminal that would be produced by that production
let prodIdxToNonTerminal (prodIdx:int) = 
  match prodIdx with
    | 0 -> NONTERM__startConfiguration 
    | 1 -> NONTERM_Configuration 
    | 2 -> NONTERM_Blocks 
    | 3 -> NONTERM_Blocks 
    | 4 -> NONTERM_Block 
    | 5 -> NONTERM_Block 
    | 6 -> NONTERM_Block 
    | 7 -> NONTERM_BlockBody 
    | 8 -> NONTERM_BlockAttributes 
    | 9 -> NONTERM_BlockAttributes 
    | 10 -> NONTERM_Attribute 
    | 11 -> NONTERM_Attribute 
    | 12 -> NONTERM_Attribute 
    | 13 -> NONTERM_AttributeValue 
    | 14 -> NONTERM_AttributeArray 
    | 15 -> NONTERM_ArrayValues 
    | 16 -> NONTERM_ArrayValues 
    | 17 -> NONTERM_AttributeSubBlock 
    | 18 -> NONTERM_AttributeMapValues 
    | 19 -> NONTERM_AttributeMapValues 
    | 20 -> NONTERM_Expr 
    | 21 -> NONTERM_Expr 
    | 22 -> NONTERM_Expr 
    | 23 -> NONTERM_Expr 
    | 24 -> NONTERM_Expr 
    | 25 -> NONTERM_Expr 
    | 26 -> NONTERM_Expr 
    | 27 -> NONTERM_Expr 
    | 28 -> NONTERM_Expr 
    | _ -> failwith "prodIdxToNonTerminal: bad production index"

let _fsyacc_endOfInputTag = 21 
let _fsyacc_tagOfErrorTerminal = 19

// This function gets the name of a token as a string
let token_to_string (t:token) = 
  match t with 
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
let _fsyacc_gotos = [| 0us; 65535us; 1us; 65535us; 0us; 1us; 1us; 65535us; 0us; 2us; 2us; 65535us; 2us; 4us; 13us; 25us; 4us; 65535us; 5us; 7us; 6us; 7us; 8us; 9us; 10us; 11us; 1us; 65535us; 12us; 13us; 1us; 65535us; 13us; 15us; 1us; 65535us; 13us; 16us; 1us; 65535us; 13us; 17us; 1us; 65535us; 21us; 22us; 1us; 65535us; 13us; 18us; 0us; 65535us; 6us; 65535us; 19us; 20us; 22us; 24us; 35us; 31us; 37us; 32us; 40us; 33us; 43us; 34us; |]
let _fsyacc_sparseGotoTableRowOffsets = [|0us; 1us; 3us; 5us; 8us; 13us; 15us; 17us; 19us; 21us; 23us; 25us; 26us; |]
let _fsyacc_stateToProdIdxsTableElements = [| 1us; 0us; 1us; 0us; 2us; 1us; 3us; 1us; 1us; 1us; 3us; 3us; 4us; 5us; 6us; 5us; 4us; 5us; 6us; 13us; 14us; 1us; 4us; 2us; 5us; 6us; 1us; 5us; 1us; 6us; 1us; 6us; 1us; 7us; 2us; 7us; 9us; 1us; 7us; 1us; 9us; 1us; 10us; 1us; 11us; 1us; 12us; 2us; 13us; 14us; 2us; 13us; 25us; 1us; 14us; 2us; 14us; 16us; 1us; 14us; 2us; 16us; 25us; 1us; 17us; 1us; 20us; 1us; 21us; 1us; 22us; 1us; 23us; 1us; 24us; 2us; 25us; 25us; 2us; 25us; 26us; 2us; 25us; 27us; 2us; 25us; 28us; 1us; 25us; 1us; 26us; 1us; 26us; 1us; 26us; 1us; 27us; 1us; 27us; 1us; 27us; 1us; 28us; 1us; 28us; 1us; 28us; |]
let _fsyacc_stateToProdIdxsTableRowOffsets = [|0us; 2us; 4us; 7us; 9us; 11us; 15us; 21us; 23us; 26us; 28us; 30us; 32us; 34us; 37us; 39us; 41us; 43us; 45us; 47us; 50us; 53us; 55us; 58us; 60us; 63us; 65us; 67us; 69us; 71us; 73us; 75us; 78us; 81us; 84us; 87us; 89us; 91us; 93us; 95us; 97us; 99us; 101us; 103us; 105us; |]
let _fsyacc_action_rows = 45
let _fsyacc_actionTableElements = [|0us; 16386us; 0us; 49152us; 2us; 32768us; 0us; 3us; 14us; 5us; 0us; 16385us; 0us; 16387us; 2us; 32768us; 11us; 12us; 14us; 8us; 3us; 32768us; 6us; 19us; 11us; 12us; 14us; 8us; 0us; 16388us; 2us; 32768us; 11us; 12us; 15us; 10us; 0us; 16389us; 1us; 32768us; 11us; 12us; 0us; 16390us; 0us; 16392us; 2us; 32768us; 12us; 14us; 14us; 6us; 0us; 16391us; 0us; 16393us; 0us; 16394us; 0us; 16395us; 0us; 16396us; 9us; 32768us; 1us; 36us; 2us; 39us; 3us; 42us; 9us; 21us; 13us; 30us; 15us; 29us; 16us; 26us; 17us; 27us; 18us; 28us; 1us; 16397us; 4us; 35us; 0us; 16399us; 9us; 32768us; 1us; 36us; 2us; 39us; 3us; 42us; 10us; 23us; 13us; 30us; 15us; 29us; 16us; 26us; 17us; 27us; 18us; 28us; 0us; 16398us; 1us; 16400us; 4us; 35us; 0us; 16401us; 0us; 16404us; 0us; 16405us; 0us; 16406us; 0us; 16407us; 0us; 16408us; 0us; 16409us; 2us; 32768us; 4us; 35us; 8us; 38us; 2us; 32768us; 4us; 35us; 8us; 41us; 2us; 32768us; 4us; 35us; 8us; 44us; 8us; 32768us; 1us; 36us; 2us; 39us; 3us; 42us; 13us; 30us; 15us; 29us; 16us; 26us; 17us; 27us; 18us; 28us; 1us; 32768us; 7us; 37us; 8us; 32768us; 1us; 36us; 2us; 39us; 3us; 42us; 13us; 30us; 15us; 29us; 16us; 26us; 17us; 27us; 18us; 28us; 0us; 16410us; 1us; 32768us; 7us; 40us; 8us; 32768us; 1us; 36us; 2us; 39us; 3us; 42us; 13us; 30us; 15us; 29us; 16us; 26us; 17us; 27us; 18us; 28us; 0us; 16411us; 1us; 32768us; 7us; 43us; 8us; 32768us; 1us; 36us; 2us; 39us; 3us; 42us; 13us; 30us; 15us; 29us; 16us; 26us; 17us; 27us; 18us; 28us; 0us; 16412us; |]
let _fsyacc_actionTableRowOffsets = [|0us; 1us; 2us; 5us; 6us; 7us; 10us; 14us; 15us; 18us; 19us; 21us; 22us; 23us; 26us; 27us; 28us; 29us; 30us; 31us; 41us; 43us; 44us; 54us; 55us; 57us; 58us; 59us; 60us; 61us; 62us; 63us; 64us; 67us; 70us; 73us; 82us; 84us; 93us; 94us; 96us; 105us; 106us; 108us; 117us; |]
let _fsyacc_reductionSymbolCounts = [|1us; 2us; 0us; 2us; 2us; 3us; 4us; 3us; 0us; 2us; 1us; 1us; 1us; 3us; 5us; 0us; 2us; 1us; 0us; 4us; 1us; 1us; 1us; 1us; 1us; 3us; 4us; 4us; 4us; |]
let _fsyacc_productionToNonTerminalTable = [|0us; 1us; 2us; 2us; 3us; 3us; 3us; 4us; 5us; 5us; 6us; 6us; 6us; 7us; 8us; 9us; 9us; 10us; 11us; 11us; 12us; 12us; 12us; 12us; 12us; 12us; 12us; 12us; 12us; |]
let _fsyacc_immediateActions = [|65535us; 49152us; 65535us; 16385us; 16387us; 65535us; 65535us; 16388us; 65535us; 16389us; 65535us; 16390us; 65535us; 65535us; 16391us; 16393us; 16394us; 16395us; 16396us; 65535us; 65535us; 65535us; 65535us; 16398us; 65535us; 16401us; 16404us; 16405us; 16406us; 16407us; 16408us; 65535us; 65535us; 65535us; 65535us; 65535us; 65535us; 65535us; 16410us; 65535us; 65535us; 16411us; 65535us; 65535us; 16412us; |]
let _fsyacc_reductions ()  =    [| 
# 221 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> AST.Blocks in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
                      raise (FSharp.Text.Parsing.Accept(Microsoft.FSharp.Core.Operators.box _1))
                   )
                 : 'gentype__startConfiguration));
# 230 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> AST.Blocks in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 44 "Parser.fsy"
                                                 _1 
                   )
# 44 "Parser.fsy"
                 : AST.Blocks));
# 241 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 46 "Parser.fsy"
                                           [] 
                   )
# 46 "Parser.fsy"
                 : AST.Blocks));
# 251 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> AST.Blocks in
            let _2 = parseState.GetInput(2) :?> AST.Attribute in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 47 "Parser.fsy"
                                            _1 @ [_2] 
                   )
# 47 "Parser.fsy"
                 : AST.Blocks));
# 263 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            let _2 = parseState.GetInput(2) :?> AST.Blocks in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 49 "Parser.fsy"
                                                   Block (_1, _2) 
                   )
# 49 "Parser.fsy"
                 : AST.Attribute));
# 275 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            let _2 = parseState.GetInput(2) :?> string in
            let _3 = parseState.GetInput(3) :?> AST.Blocks in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 50 "Parser.fsy"
                                                              BlockWithType (_1, _2, _3) 
                   )
# 50 "Parser.fsy"
                 : AST.Attribute));
# 288 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            let _2 = parseState.GetInput(2) :?> string in
            let _3 = parseState.GetInput(3) :?> string in
            let _4 = parseState.GetInput(4) :?> AST.Blocks in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 51 "Parser.fsy"
                                                                     BlockWithTypeAndName (_1, _2, _3, _4) 
                   )
# 51 "Parser.fsy"
                 : AST.Attribute));
# 302 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _2 = parseState.GetInput(2) :?> AST.Blocks in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 53 "Parser.fsy"
                                                                _2 
                   )
# 53 "Parser.fsy"
                 : AST.Blocks));
# 313 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 55 "Parser.fsy"
                                                    [] 
                   )
# 55 "Parser.fsy"
                 : AST.Blocks));
# 323 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> AST.Blocks in
            let _2 = parseState.GetInput(2) :?> AST.Attribute in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 56 "Parser.fsy"
                                                                  _1 @ [_2] 
                   )
# 56 "Parser.fsy"
                 : AST.Blocks));
# 335 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> AST.Attribute in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 58 "Parser.fsy"
                                                 _1 
                   )
# 58 "Parser.fsy"
                 : AST.Attribute));
# 346 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> AST.Attribute in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 59 "Parser.fsy"
                                                 _1 
                   )
# 59 "Parser.fsy"
                 : AST.Attribute));
# 357 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> AST.Attribute in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 60 "Parser.fsy"
                                                    _1 
                   )
# 60 "Parser.fsy"
                 : AST.Attribute));
# 368 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            let _3 = parseState.GetInput(3) :?> AST.Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 62 "Parser.fsy"
                                                               Value (_1, _3) 
                   )
# 62 "Parser.fsy"
                 : AST.Attribute));
# 380 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            let _4 = parseState.GetInput(4) :?> 'gentype_ArrayValues in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 64 "Parser.fsy"
                                                                                          Array (_1, _4) 
                   )
# 64 "Parser.fsy"
                 : AST.Attribute));
# 392 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 65 "Parser.fsy"
                                                [] 
                   )
# 65 "Parser.fsy"
                 : 'gentype_ArrayValues));
# 402 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_ArrayValues in
            let _2 = parseState.GetInput(2) :?> AST.Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 66 "Parser.fsy"
                                                     _1 @ [_2] 
                   )
# 66 "Parser.fsy"
                 : 'gentype_ArrayValues));
# 414 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> AST.Attribute in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 68 "Parser.fsy"
                                                _1 
                   )
# 68 "Parser.fsy"
                 : AST.Attribute));
# 425 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 70 "Parser.fsy"
                                                       Map.empty 
                   )
# 70 "Parser.fsy"
                 : 'gentype_AttributeMapValues));
# 435 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_AttributeMapValues in
            let _2 = parseState.GetInput(2) :?> string in
            let _4 = parseState.GetInput(4) :?> AST.Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 71 "Parser.fsy"
                                                                                    _1 |> Map.add _2 _4 
                   )
# 71 "Parser.fsy"
                 : 'gentype_AttributeMapValues));
# 448 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 73 "Parser.fsy"
                                     Nothing 
                   )
# 73 "Parser.fsy"
                 : AST.Expr));
# 458 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 74 "Parser.fsy"
                                  Boolean true 
                   )
# 74 "Parser.fsy"
                 : AST.Expr));
# 468 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 75 "Parser.fsy"
                                   Boolean false 
                   )
# 75 "Parser.fsy"
                 : AST.Expr));
# 478 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 76 "Parser.fsy"
                                    String _1 
                   )
# 76 "Parser.fsy"
                 : AST.Expr));
# 489 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 77 "Parser.fsy"
                                      Variable _1 
                   )
# 77 "Parser.fsy"
                 : AST.Expr));
# 500 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> AST.Expr in
            let _3 = parseState.GetInput(3) :?> AST.Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 78 "Parser.fsy"
                                            InfixFunction (_1, Plus, _3) 
                   )
# 78 "Parser.fsy"
                 : AST.Expr));
# 512 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> AST.Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 79 "Parser.fsy"
                                                     Function (Trim, _3) 
                   )
# 79 "Parser.fsy"
                 : AST.Expr));
# 523 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> AST.Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 80 "Parser.fsy"
                                                      Function (Upper, _3) 
                   )
# 80 "Parser.fsy"
                 : AST.Expr));
# 534 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> AST.Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 81 "Parser.fsy"
                                                      Function (Lower, _3) 
                   )
# 81 "Parser.fsy"
                 : AST.Expr));
|]
# 546 "Parser.fs"
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
    numTerminals = 22;
    productionToNonTerminalTable = _fsyacc_productionToNonTerminalTable  }
let engine lexer lexbuf startState = tables.Interpret(lexer, lexbuf, startState)
let Configuration lexer lexbuf : AST.Blocks =
    engine lexer lexbuf 0 :?> _
