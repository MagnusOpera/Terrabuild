module Yaml
open System
open System.IO
open Collections
open MagnusOpera.PresqueYaml
open System.Text


let loadDocument filename =
    try
        let yaml = System.IO.File.ReadAllText filename
        let model = YamlParser.Read yaml
        model |> Ok
    with
        | ex -> Error ex

let toString (node: YamlNode) =
    match node with
    | YamlNode.Scalar value -> value
    | YamlNode.None -> null
    | _ -> failwithf "Cannot extract string from node %A" node

let toOptionalString (node:YamlNode option) =
    node
    |> Option.map toString

let toInt (node: YamlNode) =
    match node with
    | YamlNode.Scalar value -> Convert.ToInt32 value
    | _ -> failwithf "Cannot extract int from node %A" node

let toOptionalInt (node:YamlNode option) =
    node
    |> Option.map toInt

let toBool (node: YamlNode) =
    match node with
    | YamlNode.Scalar value -> Convert.ToBoolean value
    | _ -> failwithf "Cannot extract int from node %A" node

let toOptionalBool (node: YamlNode option) =
    node
    |> Option.map toBool

let toList mapper = function
    | YamlNode.Sequence sequence -> sequence |> List.map mapper
    | _ -> failwith "Expecting sequence"

let toOptionalList mapper = function
    | Some node -> node |> toList mapper
    | None -> List.empty

let toNodeList node = toList id node

let toStringList node = toList toString node

let toOptionalStringList node = toOptionalList toString node

let toMap mapper = function
    | YamlNode.Mapping mapping -> mapping
    | _ -> failwith "Expecting mapping"

let toOptionalMap mapper = function
    | Some node -> node |> toMap mapper
    | None -> Map.empty

let toStringMap node = toMap toString node

let toOptionalStringMap node = toOptionalMap toString node

let child name (node: YamlNode) =
    match node with
    | YamlNode.Mapping mapping -> Some mapping[name]
    | _ -> None

let query (path: string) (node: YamlNode) =
    let rec find sections (node:YamlNode) =
        match sections with
        | head :: tail ->
            match node with
            | YamlNode.Mapping mapping -> mapping |> Map.tryFind head |> Option.bind (find tail)
            | _ -> None
        | _ -> Some node

    let sections =
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
        |> List.ofArray
    find sections node

let queryList path mapper node =
    node
    |> query path 
    |> toOptionalList mapper

let queryMap<'t> path mapper node =
    node
    |> query path
    |> toOptionalMap mapper


let deserialize<'T>(node: YamlNode): 'T =
    YamlSerializer.Deserialize<'T>(node)

let deserializeType(returnType:Type, node: YamlNode): obj =
    YamlSerializer.Deserialize(node, returnType)


let dumpAsString (node: YamlNode) =
    let sb = StringBuilder()

    let rec writeNode (node: YamlNode) =
        match node with
        | YamlNode.None -> ()
        | YamlNode.Scalar scalar -> sb.AppendLine(scalar) |> ignore
        | YamlNode.Sequence sequence ->
            for item in sequence do
                sb.Append($"- ") |> ignore
                writeNode item
        | YamlNode.Mapping mapping ->
            for (KeyValue(name, item)) in mapping do
                sb.Append($"{name}:") |> ignore
                writeNode item

    writeNode node
    sb.ToString()
