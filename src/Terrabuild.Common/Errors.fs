module Errors
open System


// Generic exception
type TerrabuildException(msg, ?innerException: Exception) =
    inherit Exception(msg, innerException |> Option.defaultValue null)

// parser exception
type ParseException(msg) =
    inherit TerrabuildException(msg)

type TypeException(msg) =
    inherit TerrabuildException(msg)

type SymbolException(msg) =
    inherit TerrabuildException(msg)

// CLI exception
type UsageException(msg) =
    inherit TerrabuildException(msg)

// Invalid argument
type InvalidArgumentException(msg) =
    inherit TerrabuildException(msg)

let raiseInvalidArg msg =
    InvalidArgumentException(msg) |> raise

let raiseUsage msg =
    UsageException(msg) |> raise

let raiseParseError msg =
    ParseException(msg) |> raise

let raiseTypeError msg =
    TypeException(msg) |> raise

let raiseSymbolError msg =
    SymbolException(msg) |> raise

let raiseGenericError msg =
    failwith msg

let forwardError msg innerException =
    TerrabuildException(msg, innerException) |> raise
