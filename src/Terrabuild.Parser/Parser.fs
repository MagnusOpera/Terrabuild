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
    | NONTERM_BlockHeader
    | NONTERM_BlockBody
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
    | 5 -> NONTERM_BlockHeader 
    | 6 -> NONTERM_BlockHeader 
    | 7 -> NONTERM_BlockHeader 
    | 8 -> NONTERM_BlockBody 
    | 9 -> NONTERM_BlockBody 
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
let _fsyacc_gotos = [| 0us; 65535us; 1us; 65535us; 0us; 1us; 1us; 65535us; 0us; 2us; 2us; 65535us; 2us; 4us; 7us; 23us; 2us; 65535us; 2us; 5us; 7us; 5us; 1us; 65535us; 6us; 7us; 1us; 65535us; 7us; 13us; 1us; 65535us; 7us; 14us; 1us; 65535us; 7us; 15us; 1us; 65535us; 19us; 20us; 1us; 65535us; 7us; 16us; 0us; 65535us; 6us; 65535us; 17us; 18us; 20us; 22us; 33us; 29us; 35us; 30us; 38us; 31us; 41us; 32us; |]
let _fsyacc_sparseGotoTableRowOffsets = [|0us; 1us; 3us; 5us; 8us; 11us; 13us; 15us; 17us; 19us; 21us; 23us; 24us; |]
let _fsyacc_stateToProdIdxsTableElements = [| 1us; 0us; 1us; 0us; 2us; 1us; 3us; 1us; 1us; 1us; 3us; 1us; 4us; 1us; 4us; 2us; 4us; 9us; 1us; 4us; 3us; 5us; 6us; 7us; 5us; 5us; 6us; 7us; 13us; 14us; 2us; 6us; 7us; 1us; 7us; 1us; 9us; 1us; 10us; 1us; 11us; 1us; 12us; 2us; 13us; 14us; 2us; 13us; 25us; 1us; 14us; 2us; 14us; 16us; 1us; 14us; 2us; 16us; 25us; 1us; 17us; 1us; 20us; 1us; 21us; 1us; 22us; 1us; 23us; 1us; 24us; 2us; 25us; 25us; 2us; 25us; 26us; 2us; 25us; 27us; 2us; 25us; 28us; 1us; 25us; 1us; 26us; 1us; 26us; 1us; 26us; 1us; 27us; 1us; 27us; 1us; 27us; 1us; 28us; 1us; 28us; 1us; 28us; |]
let _fsyacc_stateToProdIdxsTableRowOffsets = [|0us; 2us; 4us; 7us; 9us; 11us; 13us; 15us; 18us; 20us; 24us; 30us; 33us; 35us; 37us; 39us; 41us; 43us; 46us; 49us; 51us; 54us; 56us; 59us; 61us; 63us; 65us; 67us; 69us; 71us; 74us; 77us; 80us; 83us; 85us; 87us; 89us; 91us; 93us; 95us; 97us; 99us; 101us; |]
let _fsyacc_action_rows = 43
let _fsyacc_actionTableElements = [|0us; 16386us; 0us; 49152us; 2us; 32768us; 0us; 3us; 14us; 9us; 0us; 16385us; 0us; 16387us; 1us; 32768us; 11us; 6us; 0us; 16392us; 2us; 32768us; 12us; 8us; 14us; 10us; 0us; 16388us; 1us; 16389us; 14us; 11us; 2us; 16389us; 6us; 17us; 14us; 11us; 1us; 16390us; 15us; 12us; 0us; 16391us; 0us; 16393us; 0us; 16394us; 0us; 16395us; 0us; 16396us; 9us; 32768us; 1us; 34us; 2us; 37us; 3us; 40us; 9us; 19us; 13us; 28us; 15us; 27us; 16us; 24us; 17us; 25us; 18us; 26us; 1us; 16397us; 4us; 33us; 0us; 16399us; 9us; 32768us; 1us; 34us; 2us; 37us; 3us; 40us; 10us; 21us; 13us; 28us; 15us; 27us; 16us; 24us; 17us; 25us; 18us; 26us; 0us; 16398us; 1us; 16400us; 4us; 33us; 0us; 16401us; 0us; 16404us; 0us; 16405us; 0us; 16406us; 0us; 16407us; 0us; 16408us; 0us; 16409us; 2us; 32768us; 4us; 33us; 8us; 36us; 2us; 32768us; 4us; 33us; 8us; 39us; 2us; 32768us; 4us; 33us; 8us; 42us; 8us; 32768us; 1us; 34us; 2us; 37us; 3us; 40us; 13us; 28us; 15us; 27us; 16us; 24us; 17us; 25us; 18us; 26us; 1us; 32768us; 7us; 35us; 8us; 32768us; 1us; 34us; 2us; 37us; 3us; 40us; 13us; 28us; 15us; 27us; 16us; 24us; 17us; 25us; 18us; 26us; 0us; 16410us; 1us; 32768us; 7us; 38us; 8us; 32768us; 1us; 34us; 2us; 37us; 3us; 40us; 13us; 28us; 15us; 27us; 16us; 24us; 17us; 25us; 18us; 26us; 0us; 16411us; 1us; 32768us; 7us; 41us; 8us; 32768us; 1us; 34us; 2us; 37us; 3us; 40us; 13us; 28us; 15us; 27us; 16us; 24us; 17us; 25us; 18us; 26us; 0us; 16412us; |]
let _fsyacc_actionTableRowOffsets = [|0us; 1us; 2us; 5us; 6us; 7us; 9us; 10us; 13us; 14us; 16us; 19us; 21us; 22us; 23us; 24us; 25us; 26us; 36us; 38us; 39us; 49us; 50us; 52us; 53us; 54us; 55us; 56us; 57us; 58us; 59us; 62us; 65us; 68us; 77us; 79us; 88us; 89us; 91us; 100us; 101us; 103us; 112us; |]
let _fsyacc_reductionSymbolCounts = [|1us; 2us; 0us; 2us; 4us; 1us; 2us; 3us; 0us; 2us; 1us; 1us; 1us; 3us; 5us; 0us; 2us; 1us; 0us; 4us; 1us; 1us; 1us; 1us; 1us; 3us; 4us; 4us; 4us; |]
let _fsyacc_productionToNonTerminalTable = [|0us; 1us; 2us; 2us; 3us; 4us; 4us; 4us; 5us; 5us; 6us; 6us; 6us; 7us; 8us; 9us; 9us; 10us; 11us; 11us; 12us; 12us; 12us; 12us; 12us; 12us; 12us; 12us; 12us; |]
let _fsyacc_immediateActions = [|65535us; 49152us; 65535us; 16385us; 16387us; 65535us; 65535us; 65535us; 16388us; 65535us; 65535us; 65535us; 16391us; 16393us; 16394us; 16395us; 16396us; 65535us; 65535us; 65535us; 65535us; 16398us; 65535us; 16401us; 16404us; 16405us; 16406us; 16407us; 16408us; 65535us; 65535us; 65535us; 65535us; 65535us; 65535us; 65535us; 16410us; 65535us; 65535us; 16411us; 65535us; 65535us; 16412us; |]
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
# 43 "Parser.fsy"
                                                 _1 
                   )
# 43 "Parser.fsy"
                 : AST.Blocks));
# 241 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 45 "Parser.fsy"
                                           [] 
                   )
# 45 "Parser.fsy"
                 : AST.Blocks));
# 251 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> AST.Blocks in
            let _2 = parseState.GetInput(2) :?> 'gentype_Block in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 46 "Parser.fsy"
                                            _1 @ [_2] 
                   )
# 46 "Parser.fsy"
                 : AST.Blocks));
# 263 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> AST.BlockHeader in
            let _3 = parseState.GetInput(3) :?> AST.BlockBody in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 48 "Parser.fsy"
                                                                  { Header = _1; Body = _3 } 
                   )
# 48 "Parser.fsy"
                 : 'gentype_Block));
# 275 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 50 "Parser.fsy"
                                               Block (_1) 
                   )
# 50 "Parser.fsy"
                 : AST.BlockHeader));
# 286 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            let _2 = parseState.GetInput(2) :?> string in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 51 "Parser.fsy"
                                                          BlockName (_1, _2) 
                   )
# 51 "Parser.fsy"
                 : AST.BlockHeader));
# 298 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            let _2 = parseState.GetInput(2) :?> string in
            let _3 = parseState.GetInput(3) :?> string in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 52 "Parser.fsy"
                                                                 BlockTypeName (_1, _2, _3) 
                   )
# 52 "Parser.fsy"
                 : AST.BlockHeader));
# 311 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 54 "Parser.fsy"
                                              [] 
                   )
# 54 "Parser.fsy"
                 : AST.BlockBody));
# 321 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> AST.BlockBody in
            let _2 = parseState.GetInput(2) :?> AST.Attribute in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 55 "Parser.fsy"
                                                      _1 @ [_2] 
                   )
# 55 "Parser.fsy"
                 : AST.BlockBody));
# 333 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> AST.Attribute in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 57 "Parser.fsy"
                                                 _1 
                   )
# 57 "Parser.fsy"
                 : AST.Attribute));
# 344 "Parser.fs"
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
# 355 "Parser.fs"
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
# 366 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            let _3 = parseState.GetInput(3) :?> AST.Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 61 "Parser.fsy"
                                                               Value (_1, _3) 
                   )
# 61 "Parser.fsy"
                 : AST.Attribute));
# 378 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            let _4 = parseState.GetInput(4) :?> 'gentype_ArrayValues in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 63 "Parser.fsy"
                                                                                          Array (_1, _4) 
                   )
# 63 "Parser.fsy"
                 : AST.Attribute));
# 390 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 64 "Parser.fsy"
                                                [] 
                   )
# 64 "Parser.fsy"
                 : 'gentype_ArrayValues));
# 400 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_ArrayValues in
            let _2 = parseState.GetInput(2) :?> AST.Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 65 "Parser.fsy"
                                                     _1 @ [_2] 
                   )
# 65 "Parser.fsy"
                 : 'gentype_ArrayValues));
# 412 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_Block in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 67 "Parser.fsy"
                                                SubBlock _1 
                   )
# 67 "Parser.fsy"
                 : AST.Attribute));
# 423 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 69 "Parser.fsy"
                                                       Map.empty 
                   )
# 69 "Parser.fsy"
                 : 'gentype_AttributeMapValues));
# 433 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> 'gentype_AttributeMapValues in
            let _2 = parseState.GetInput(2) :?> string in
            let _4 = parseState.GetInput(4) :?> AST.Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 70 "Parser.fsy"
                                                                                    _1 |> Map.add _2 _4 
                   )
# 70 "Parser.fsy"
                 : 'gentype_AttributeMapValues));
# 446 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 72 "Parser.fsy"
                                     Nothing 
                   )
# 72 "Parser.fsy"
                 : AST.Expr));
# 456 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 73 "Parser.fsy"
                                  Boolean true 
                   )
# 73 "Parser.fsy"
                 : AST.Expr));
# 466 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 74 "Parser.fsy"
                                   Boolean false 
                   )
# 74 "Parser.fsy"
                 : AST.Expr));
# 476 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 75 "Parser.fsy"
                                    String _1 
                   )
# 75 "Parser.fsy"
                 : AST.Expr));
# 487 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> string in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 76 "Parser.fsy"
                                      Variable _1 
                   )
# 76 "Parser.fsy"
                 : AST.Expr));
# 498 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _1 = parseState.GetInput(1) :?> AST.Expr in
            let _3 = parseState.GetInput(3) :?> AST.Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 77 "Parser.fsy"
                                            InfixFunction (_1, Plus, _3) 
                   )
# 77 "Parser.fsy"
                 : AST.Expr));
# 510 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> AST.Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 78 "Parser.fsy"
                                                     Function (Trim, _3) 
                   )
# 78 "Parser.fsy"
                 : AST.Expr));
# 521 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> AST.Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 79 "Parser.fsy"
                                                      Function (Upper, _3) 
                   )
# 79 "Parser.fsy"
                 : AST.Expr));
# 532 "Parser.fs"
        (fun (parseState : FSharp.Text.Parsing.IParseState) ->
            let _3 = parseState.GetInput(3) :?> AST.Expr in
            Microsoft.FSharp.Core.Operators.box
                (
                   (
# 80 "Parser.fsy"
                                                      Function (Lower, _3) 
                   )
# 80 "Parser.fsy"
                 : AST.Expr));
|]
# 544 "Parser.fs"
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
