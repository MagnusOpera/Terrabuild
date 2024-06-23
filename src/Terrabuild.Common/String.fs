module String

open System
open System.IO
open System.Text.RegularExpressions


let toLower (s : string) =
    s.ToLowerInvariant()

let join (separator : string) (strings : string seq) =
    String.Join(separator, strings)

let firstLine (input: string) =
    use reader = new StringReader(input)
    reader.ReadLine()

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
