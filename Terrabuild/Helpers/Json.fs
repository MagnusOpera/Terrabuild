module Json
open System.Text.Json
open System.Text.Json.Serialization

let private settings =
    let settings =
        JsonFSharpOptions.ThothLike()
                         .WithUnionTagNamingPolicy(JsonNamingPolicy.CamelCase)
                         .ToJsonSerializerOptions()
    settings.WriteIndented <- true
    settings.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    settings

let Serialize (value: obj)=
    JsonSerializer.Serialize(value, settings)

let Deserialize<'t> (json: string) =
    JsonSerializer.Deserialize<'t>(json, settings)
