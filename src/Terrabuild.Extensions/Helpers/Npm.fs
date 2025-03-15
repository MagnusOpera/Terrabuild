module NpmHelpers

open System.Text.Json.Serialization
open System.Text.Json
open System.Collections.Generic
open Errors

[<CLIMutable>]
type Package = {
    [<JsonPropertyName("dependencies")>]
    Dependencies: Dictionary<string, string>

    [<JsonPropertyName("devDependencies")>]
    DevDependencies: Dictionary<string, string>
}

let findProjectFile (directory: string) =
    let projects =
        System.IO.Directory.EnumerateFiles(directory, "package.json")
        |> List.ofSeq
    match projects with
    | [ project ] -> project
    | [] -> raiseInvalidArg "No project found"
    | _ -> raiseInvalidArg "Multiple projects found"

let findDependencies (projectFile: string) =
    let json = IO.readTextFile projectFile
    let package = JsonSerializer.Deserialize<Package>(json)

    let dependencies = seq {
        if package.Dependencies|> isNull |> not then
            for (KeyValue(_, value)) in package.Dependencies do
                match value with
                | String.Regex "^file:(.*)$" [project] -> yield project
                | _ -> ()

        if package.DevDependencies|> isNull |> not then
            for (KeyValue(_, value)) in package.DevDependencies do
                match value with
                | String.Regex "^file:(.*)$" [project] -> yield project
                | _ -> ()
    }
    Set dependencies
