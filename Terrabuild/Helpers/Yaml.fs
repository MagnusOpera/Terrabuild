module Helpers.Yaml
open Legivel.Serialization

let private yamlOptions = [ MappingMode(MapYaml.AndRequireFullProjection) ]

let DeserializeFile<'t> filename =
    let content = filename |> IO.readTextFile

    match DeserializeWithOptions<'t> yamlOptions content with
    | [ Success x ] -> x.Data
    | x -> failwith $"failed to deserialize file '{filename}:\n{x}"
