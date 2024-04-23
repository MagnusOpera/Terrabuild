namespace Contracts
open System


type ConfigException(msg, ?innerException: Exception) =
    inherit Exception(msg, innerException |> Option.defaultValue null)

    static member Raise(msg, ?innerException) =
        ConfigException(msg, ?innerException=innerException) |> raise
