module Helpers.Json
open System.IO
open Legivel.Serialization

let private yamlOptions = [ MappingMode(MapYaml.AndRequireFullProjection) ]

let DeserializeFile<'t> filename =
    let content = File.ReadAllText(filename)

    match DeserializeWithOptions<'t> yamlOptions content with
    | [ Success x ] -> x.Data
    | _ -> failwith "failed to deserialize"
