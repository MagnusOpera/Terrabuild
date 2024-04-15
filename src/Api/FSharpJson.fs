module FSharpJson
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.Extensions.Configuration
open System.Text.Json.Nodes
open System
open System.Collections.Generic


let Configure (options: JsonSerializerOptions) =
    options.WriteIndented <- true
    options.ReadCommentHandling <- JsonCommentHandling.Skip
    options.AllowTrailingCommas <- true
    options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    options.PropertyNameCaseInsensitive <- true    
    options.Converters.Clear()
    options.Converters.Add(JsonFSharpConverter(JsonUnionEncoding.FSharpLuLike, unionTagCaseInsensitive = true))

let Settings =
    let options = JsonSerializerOptions()
    Configure options
    options

let Serialize (value: obj)=
    JsonSerializer.Serialize(value, Settings)

let Deserialize<'t> (json: string) =
    JsonSerializer.Deserialize<'t>(json, Settings)


let rec ToJson (section: IConfigurationSection): JsonNode =
    let children = section.GetChildren() |> Array.ofSeq
    if children.Length = 0 then
        match bool.TryParse(section.Value) with
        | true, bool -> JsonValue.Create(bool)
        | _ ->
            match Decimal.TryParse(section.Value) with
            | true, decimal -> JsonValue.Create(decimal)
            | _ ->
                match Int64.TryParse(section.Value) with
                | true, int64 -> JsonValue.Create(int64)
                | _ -> JsonValue.Create(section.Value)
    elif children[0].Path.EndsWith(":0") then
        let arrElement = children |> Array.map ToJson |> JsonArray
        arrElement
    else
        let kvpOf (child: IConfigurationSection) = KeyValuePair(child.Key, ToJson child)
        let objElement = children |> Array.map kvpOf |> JsonObject
        objElement
