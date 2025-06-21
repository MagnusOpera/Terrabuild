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

let expandHome (input: string) (terrabuildHome: string) : string =
    // Match either $HOME or ${HOME} not followed by a letter/underscore/digit
    let pattern = @"(?<!\w)\$(HOME)(?![\w])|\$\{HOME\}"
    Regex.Replace(input, pattern, terrabuildHome)
