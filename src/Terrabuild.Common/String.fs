module String

open System
open System.IO
open System.Text.RegularExpressions


let toLower (s : string) =
    s.ToLowerInvariant()

let join (separator : string) (strings : string seq) =
    String.Join(separator, strings)

let firstLine (input: string) =
    input.Split([| "\r\n"; "\n" |], StringSplitOptions.None)[0]

let getLines (input: string) =
    input.Split([| "\r\n"; "\n" |], StringSplitOptions.RemoveEmptyEntries)

let (|Regex|_|) pattern input =
    let m = Regex.Match(input, pattern)
    if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
    else None

let cut m (s: string) =
    if s.Length > m then s.Substring(0, m) + "..."
    else s

let startsWith (start: string) (s: string) =
    s.StartsWith(start)

let trim (s: string) =
    s.Trim()

let replace (substring: string) (value: string) (s: string) =
    s.Replace(substring, value)
