module Environment
open System
open System.Collections
open System.Text.RegularExpressions

let envVar (varName: string) =
    varName |> Environment.GetEnvironmentVariable |> Option.ofObj

let currentDir() = System.Environment.CurrentDirectory

let envVars() =
    Environment.GetEnvironmentVariables()
    |> Seq.cast<DictionaryEntry>
    |> Seq.map (fun entry -> $"{entry.Key}", $"{entry.Value}")
    |> Map.ofSeq

let expandTerrabuildHome (input: string) (terrabuildHome: string) : string =
    input.Replace("$TERRABUILD_HOME", terrabuildHome)
         .Replace("${TERRABUILD_HOME}", terrabuildHome)
         .Replace("~", terrabuildHome)
