module String

open System
open Microsoft.FSharp.Reflection
open System.IO
open System.Text.RegularExpressions


let toLowerInvariant (s : string) =
    s.ToLowerInvariant()

let split (splitWith : string) (s : string) =
    s.Split([|splitWith|], StringSplitOptions.RemoveEmptyEntries)

let trim (trimChar : char) (s : string) =
    s.Trim(trimChar)

let join (separator : string) (strings : string seq) =
    String.Join(separator, strings)

let contains (findWhat:string) (value:string) =
    value.Contains(findWhat)

let replace (replaceWhat:string) (replaceBy:string) (value:string) =
    value.Replace(replaceWhat, replaceBy)

let toString (x:'a) =
    match FSharpValue.GetUnionFields(x, typeof<'a>) with
    | case, _ -> case.Name.ToLowerInvariant()

let isEmpty (s: string) =
    String.IsNullOrWhiteSpace(s)

let firstLine (input: string) =
    use reader = new StringReader(input)
    reader.ReadLine()

let (|Regex|_|) pattern input =
    let m = Regex.Match(input, pattern)
    if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
    else None

let AllMatches pattern input =
    let ms = Regex.Matches(input, pattern)
    [   for m in ms do
            for g in m.Groups |> Seq.tail do
                for c in g.Captures do
                    c.Value
    ]

let (|Integer|_|) (s: string) =
    match Int32.TryParse(s) with
    | true, i -> Some i
    | _ -> None

let cut m (s: string) =
    if s.Length > m then s.Substring(0, m) + "..."
    else s
