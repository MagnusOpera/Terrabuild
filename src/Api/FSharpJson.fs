module FSharpJson
open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.Extensions.Configuration
open System.Text.Json.Nodes
open System


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


let rec ToJson (config: IConfiguration): JsonNode =
    match config.GetChildren() |> Seq.tryHead with
    | Some child when child.Path.EndsWith(":0") ->
        let arrElement = JsonArray()
        for arrayChild in config.GetChildren() do
            arrElement.Add(ToJson arrayChild)
        arrElement
    | _ ->
        let objElement = JsonObject()
        for child in config.GetChildren() do
            objElement.Add(child.Key, ToJson child)
        match config with
        | :? IConfigurationSection as section when objElement.Count = 0 ->
            match bool.TryParse(section.Value) with
            | true, boolean -> JsonValue.Create(boolean)
            | _ ->
                match Decimal.TryParse(section.Value) with
                | true, decimal -> JsonValue.Create(decimal)
                | _ ->
                    match Int64.TryParse(section.Value) with
                    | true, int -> JsonValue.Create(int)
                    | _ -> JsonValue.Create(section.Value)
        | _ -> objElement

