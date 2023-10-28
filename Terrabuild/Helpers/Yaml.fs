module Yaml
open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions
open YamlDotNet.RepresentationModel
open YamlDotNet.Core.Events
open System
open YamlDotNet.Core
open System.IO
open Collections


// from https://stackoverflow.com/questions/46697298/whats-the-best-way-to-parse-yaml-in-f-on-net-core
let (|Mapping|Scalar|Sequence|) (yamlNode: YamlNode) =  
    match yamlNode.NodeType with    
    | YamlNodeType.Mapping  -> 
        let node = yamlNode :?> YamlMappingNode
        let mapping = 
            node.Children 
            |> Seq.map (fun kvp -> 
                let keyNode = kvp.Key :?> YamlScalarNode
                keyNode.Value, kvp.Value) 
            |> Map            
        Mapping (node, mapping)
    | YamlNodeType.Scalar   -> 
        let node = yamlNode :?> YamlScalarNode
        Scalar (node, node.Value)
    | YamlNodeType.Sequence -> 
        let node = yamlNode :?> YamlSequenceNode
        Sequence (node, List.ofSeq node.Children)
    | YamlNodeType.Alias 
    | _ -> failwith "¯\_(ツ)_/¯"


// converted to F# from https://stackoverflow.com/questions/40696644/how-to-deserialize-a-yamlnode-in-yamldotnet
let rec streamToEventStream (stream: YamlStream): ParsingEvent seq =
    seq {
        yield StreamStart()
        for document in stream.Documents do
            yield! documentToEventStream(document)
        yield StreamEnd()
    }

and documentToEventStream (document: YamlDocument): ParsingEvent seq =
    seq {
        yield DocumentStart()
        yield! nodeToEventStream(document.RootNode)
        yield DocumentEnd(false)
    }

and nodeToEventStream (node: YamlNode): ParsingEvent seq =
    seq {
        match node with
        | :? YamlScalarNode as scalar ->
            yield! scalarToEventStream(scalar)
        | :? YamlSequenceNode as sequence ->
            yield! sequenceToEventStream(sequence)
        | :? YamlMappingNode as mapping ->
            yield! mappingToEventStream(mapping)
        | _ ->
            NotSupportedException($"Unsupported node type: {node.GetType().Name}")
            |> raise
    }

and scalarToEventStream (scalar: YamlScalarNode): ParsingEvent seq =
    seq {
        yield Scalar(scalar.Anchor, scalar.Tag, scalar.Value, scalar.Style, false, false)
    }

and sequenceToEventStream (sequence: YamlSequenceNode): ParsingEvent seq =
    seq {
        yield SequenceStart(sequence.Anchor, sequence.Tag, false, sequence.Style)
        for node in sequence.Children do
            yield! nodeToEventStream(node)
        yield SequenceEnd()
    }

and mappingToEventStream (mapping: YamlMappingNode): ParsingEvent seq =
    seq {
        yield MappingStart(mapping.Anchor, mapping.Tag, false, mapping.Style)
        for pair in mapping.Children do
            yield! nodeToEventStream(pair.Key)
            yield! nodeToEventStream(pair.Value)
        yield MappingEnd()
    }

type EventStreamParserAdapter(events: ParsingEvent seq) =
    let enumerator = events.GetEnumerator()

    interface IParser with
        override _.Current = enumerator.Current
        override _.MoveNext() = enumerator.MoveNext()




let singleDocument (yamlStream: YamlStream) =
    let docCount = yamlStream.Documents.Count
    if (docCount <> 1) then failwithf "Expected 1 document, got %d" docCount
    let node =  yamlStream.Documents.[0].RootNode
    node

let loadDocument filename =
    try
        let yaml = System.IO.File.ReadAllText filename
        use input = new StringReader(yaml)
        let yamlStream = YamlStream()
        yamlStream.Load(input)
        yamlStream |> singleDocument |> Ok
    with
        | ex -> Error ex

let deserializer =
    DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build()

let loadModelFromType (target: Type) (yaml: string) =
    try
        deserializer.Deserialize(yaml, target)
    with
        exn -> failwith $"failed to deserialize content:\n{exn}"

let loadModel<'t> filename =
    try
        let content = filename |> System.IO.File.ReadAllText
        deserializer.Deserialize<'t>(content)
    with
        exn -> failwith $"failed to deserialize file {filename}:\n{exn}"

let materializeModelFromType (target: Type) (node: YamlNode) =
    try
        let parser =
            node
            |> nodeToEventStream
            |> EventStreamParserAdapter

        deserializer.Deserialize(parser, target)
    with
        exn -> failwith $"failed to deserialize model:\n{exn}"

let materializeModel<'t> (node: YamlNode) = materializeModelFromType typeof<'t> node



// converted to F# from https://stackoverflow.com/questions/51630430/getting-a-yamldotnet-sharpyaml-node-using-a-string-path-such-as-category-objec
let toString (node: YamlNode) =
    match node with
    | :? YamlScalarNode as scalar -> scalar.Value
    | _ -> failwithf "Cannot extract string from node %A" node

let toOptionalString (node:YamlNode option) =
    node
    |> Option.map toString

let toInt (node: YamlNode) =
    match node with
    | :? YamlScalarNode as scalar -> Convert.ToInt32 scalar.Value
    | _ -> failwithf "Cannot extract int from node %A" node

let toOptionalInt (node:YamlNode option) =
    node
    |> Option.map toInt

let toBool (node: YamlNode) =
    match node with
    | :? YamlScalarNode as scalar -> Convert.ToBoolean(scalar.Value)
    | _ -> failwithf "Cannot extract int from node %A" node

let toOptionalBool (node: YamlNode option) =
    node
    |> Option.map toBool

let toList mapper = function
    | Sequence (_, sequence) -> sequence |> List.map mapper
    | _ -> failwith "Expecting sequence"

let toOptionalList mapper = function
    | Some node -> node |> toList mapper
    | None -> List.empty

let toNodeList node = toList id node

let toStringList node = toList toString node

let toOptionalStringList node = toOptionalList toString node

let toMap mapper = function
    | Mapping (_, mapping) ->
        mapping
        |> Map.ofDict
        |> Map.map (fun _ value -> mapper value)
    | _ -> failwith "Expecting mapping"

let toOptionalMap mapper = function
    | Some node -> node |> toMap mapper
    | None -> Map.empty

let toStringMap node = toMap toString node

let toOptionalStringMap node = toOptionalMap toString node

let child name (node: YamlNode) =
    let key = YamlScalarNode(name)
    node[key]

let query (path: string) (node: YamlNode) =
    let rec find sections (node:YamlNode) =
        match sections with
        | head :: tail ->
            match node with
            | :? YamlMappingNode as mappingNode ->
                let key = YamlScalarNode(head)
                if mappingNode.Children.ContainsKey(key) then find tail mappingNode[key]
                else None
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
