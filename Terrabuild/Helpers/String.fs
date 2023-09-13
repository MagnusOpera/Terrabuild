module Helpers.String

open System
open Microsoft.FSharp.Reflection
open System.IO
open System.Security.Cryptography

let toLowerInvariant (s : string) =
    s.ToLowerInvariant()

let split (splitWith : string) (s : string) =
    s.Split([|splitWith|], StringSplitOptions.RemoveEmptyEntries)

let trim (trimChar : char) (s : string) =
    s.Trim(trimChar)

let join (separator : string) (strings : string seq) =
    String.Join(separator, strings)

let replace (replaceWhat:string) (replaceBy:string) (value:string) =
    value.Replace(replaceWhat, replaceBy)

let guidify (input : string) =
    use provider = System.Security.Cryptography.MD5.Create()
    let inputBytes = System.Text.Encoding.GetEncoding(0).GetBytes(input)
    let hashBytes = provider.ComputeHash(inputBytes)
    let hashGuid = Guid(hashBytes)
    hashGuid.ToString()

let toString (x:'a) =
    match FSharpValue.GetUnionFields(x, typeof<'a>) with
    | case, _ -> case.Name.ToLowerInvariant()

let isEmpty (s: string) =
    String.IsNullOrWhiteSpace(s)

let sha256 (s: string) =
    let sha256 = SHA256.Create()
    use ms = new MemoryStream()
    use txtWriter = new StreamWriter(ms)
    txtWriter.Write(s)
    txtWriter.Flush()
    ms.Position <- 0L
    let hash = ms |> sha256.ComputeHash |> Convert.ToHexString
    hash
