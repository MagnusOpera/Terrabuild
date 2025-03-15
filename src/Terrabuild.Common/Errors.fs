module Errors
open System

[<RequireQualifiedAccess>]
type ErrorArea =
    | Parse
    | Type
    | Symbol of symbol:string
    | Usage
    | InvalidArg

type TerrabuildException(msg, area, ?innerException: Exception) =
    inherit Exception(msg, innerException |> Option.defaultValue null)
    member _.Area: ErrorArea = area


let raiseInvalidArg msg =
    TerrabuildException(msg, ErrorArea.InvalidArg) |> raise

let raiseUsage msg =
    TerrabuildException(msg, ErrorArea.Usage) |> raise

let raiseParseError msg =
    TerrabuildException(msg, ErrorArea.Parse) |> raise

let raiseTypeError msg =
    TerrabuildException(msg, ErrorArea.Type) |> raise

let raiseSymbolError msg symbol =
    TerrabuildException(msg, ErrorArea.Symbol symbol) |> raise

let raiseGenericError msg =
    failwith msg

let forwardError msg innerException =
    Exception(msg, innerException) |> raise
