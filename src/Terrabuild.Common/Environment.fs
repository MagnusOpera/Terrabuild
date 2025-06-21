module Environment
open System
open System.Collections

let envVar (varName: string) =
    varName |> Environment.GetEnvironmentVariable |> Option.ofObj

let currentDir() = System.Environment.CurrentDirectory

let envVars() =
    Environment.GetEnvironmentVariables()
    |> Seq.cast<DictionaryEntry>
    |> Seq.map (fun entry -> $"{entry.Key}", $"{entry.Value}")
    |> Map.ofSeq
