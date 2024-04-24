namespace Errors
open System


type TerrabuildException(msg, ?innerException: Exception) =
    inherit Exception(msg, innerException |> Option.defaultValue null)

    static member Raise(msg, ?innerException) =
        TerrabuildException(msg, ?innerException=innerException) |> raise
