module Json
open System.Text.Json
open System.Text.Json.Serialization

let private settings =
    let options = JsonSerializerOptions(WriteIndented = true,
                                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
    JsonFSharpOptions.ThothLike()
                        .WithUnionTagNamingPolicy(JsonNamingPolicy.CamelCase)
                        .AddToJsonSerializerOptions(options)
    options

let Serialize (value: obj)=
    JsonSerializer.Serialize(value, settings)

let Deserialize<'t> (json: string) =
    JsonSerializer.Deserialize<'t>(json, settings)
