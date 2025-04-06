module Errors
open System

[<RequireQualifiedAccess>]
type ErrorArea =
    | Parse
    | Type
    | Symbol
    | InvalidArg
    | External
    | Bug
    | Auth

type TerrabuildException(msg, area, ?innerException: Exception) =
    inherit Exception(msg, innerException |> Option.toObj)
    member _.Area: ErrorArea = area


let raiseInvalidArg(msg) =
    TerrabuildException(msg, ErrorArea.InvalidArg) |> raise

let raiseParseError(msg) =
    TerrabuildException(msg, ErrorArea.Parse) |> raise

let raiseParserError(msg, innerException) =
    TerrabuildException(msg, ErrorArea.Parse, innerException) |> raise

let raiseTypeError(msg) =
    TerrabuildException(msg, ErrorArea.Type) |> raise

let raiseSymbolError(msg) =
    TerrabuildException(msg, ErrorArea.Symbol) |> raise

let raiseBugError(msg) =
    TerrabuildException(msg, ErrorArea.Bug) |> raise

let raiseExternalError(msg) =
    TerrabuildException(msg, ErrorArea.External) |> raise

let forwardExternalError(msg, innerException) =
    TerrabuildException(msg, ErrorArea.External, innerException) |> raise

let raiseAuthError(msg, innerException) =
    TerrabuildException(msg, ErrorArea.Auth, innerException) |> raise


let rec dumpKnownException (ex: Exception | null) =
    seq {
        match ex with
        | :? TerrabuildException as ex ->
            yield $"{ex.Message}"
            yield! ex.InnerException |> dumpKnownException
        | null -> ()
        | _ -> ()
    }

let getErrorArea (ex: Exception) =
    let rec getErrorArea (area: ErrorArea) (ex: Exception | null) =
        match ex with
        | :? TerrabuildException as ex -> getErrorArea ex.Area ex.InnerException
        | null -> area
        | _ -> area

    getErrorArea ErrorArea.Bug ex


let tryRun ferr faction =
    try
        faction()
    with
        exn -> ferr exn
