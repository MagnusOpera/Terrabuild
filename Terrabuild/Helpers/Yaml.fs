module Yaml
open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions

let deserializer =
    DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithDuplicateKeyChecking()
        .Build()


let DeserializeFile<'t> filename =
    try
        let content = filename |> IO.readTextFile
        deserializer.Deserialize<'t>(content)
    with
        exn -> failwith $"failed to deserialize file '{filename}:\n{exn}"
