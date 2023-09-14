module Helpers.Json
open System.Text.Json
open System.Text.Json.Serialization

let Configure (options: JsonSerializerOptions) =
    options.WriteIndented <- true
    options.Converters.Add(JsonFSharpConverter(JsonUnionEncoding.FSharpLuLike, unionTagCaseInsensitive = true))

let Settings =
    let options = JsonSerializerOptions()
    Configure options
    options

let Serialize (value: obj)=
    JsonSerializer.Serialize(value, Settings)

let Deserialize<'t> (json: string) =
    JsonSerializer.Deserialize<'t>(json, Settings)
