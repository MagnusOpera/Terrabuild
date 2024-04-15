module FSharpJson
open System.Text.Json
open System.Text.Json.Serialization


let Configure (options: JsonSerializerOptions) =
    options.WriteIndented <- true
    options.ReadCommentHandling <- JsonCommentHandling.Skip
    options.AllowTrailingCommas <- true
    options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    options.PropertyNameCaseInsensitive <- true    
    options.Converters.Clear()
    // options.Converters.Add(JsonFSharpConverter(JsonUnionEncoding.FSharpLuLike, unionTagCaseInsensitive = true))
    options.Converters.Add(JsonStringEnumConverter())

let Settings =
    let options = JsonSerializerOptions()
    Configure options
    options

let Serialize (value: obj)=
    JsonSerializer.Serialize(value, Settings)

let Deserialize<'t> (json: string) =
    JsonSerializer.Deserialize<'t>(json, Settings)
