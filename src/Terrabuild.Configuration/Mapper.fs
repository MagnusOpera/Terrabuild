module Mapper
open System
open AST
open System.Reflection
open Microsoft.FSharp.Reflection

[<AttributeUsage(AttributeTargets.Property); AllowNullLiteral>]
type KindAttribute() =
    inherit System.Attribute()

[<AttributeUsage(AttributeTargets.Property); AllowNullLiteral>]
type NameAttribute(name: string) =
    inherit System.Attribute()

    member val Name = name

[<AttributeUsage(AttributeTargets.Property); AllowNullLiteral>]
type RequiredAttribute() =
    inherit System.Attribute()

[<AttributeUsage(AttributeTargets.Property); AllowNullLiteral>]
type AnyAttribute() =
    inherit System.Attribute()



type ListOperations<'t>() =
    static member val Empty: obj =
        let empty : 't list = List.empty
        empty

    static member Add (head: 't list) (tail: 't): obj =
        let res = head @ [ tail ]
        res
        

type MapOperations<'t when 't: comparison>() =
    static member val Empty: obj =
        let empty : Map<string, 't> = Map.empty
        empty

    static member Add (map: Map<string, 't>) (key: string) (value: 't): obj =
        if map |> Map.containsKey key then failwith $"Key {key} already exists"        
        let add = map |> Map.add key value
        add


let emptyList (tpe: Type) =
    let template = typedefof<ListOperations<_>>
    let genType = template.MakeGenericType(tpe)
    let empty = genType.GetProperty("Empty")
    let inst = empty.GetValue(null)
    inst

let emptyMap (tpe: Type) =
    let template = typedefof<MapOperations<_>>
    let genType = template.MakeGenericType(tpe)
    let empty = genType.GetProperty("Empty")
    let inst = empty.GetValue(null)
    inst

let addList (tpe: Type) (head: obj) (tail: obj) =
    let template = typedefof<ListOperations<_>>
    let genType = template.MakeGenericType(tpe)
    let add = genType.GetMethod("Add")
    let res = add.Invoke(null, [| head; tail |])
    res

let addMap (tpe: Type) (m: obj) (key: string) (value: obj) =
    let template = typedefof<MapOperations<_>>
    let genType = template.MakeGenericType(tpe)
    let add = genType.GetMethod("Add")
    let res = add.Invoke(null, [| m; key; value |])
    res

let getRequiredAndDefaultValue (propInfo: PropertyInfo) =
    let genParam idx = propInfo.PropertyType.GetGenericArguments()[idx]

    let isRequired = propInfo.GetCustomAttribute<RequiredAttribute>() <> null
    match TypeHelpers.getKind propInfo.PropertyType with
    | TypeHelpers.TypeKind.FsList -> isRequired, genParam 0 |> emptyList
    | TypeHelpers.TypeKind.FsMap -> isRequired, genParam 1 |> emptyMap
    | _ -> false, null


// Map<string, Expression>
let mapMap (attributes: Attributes): obj =
    let items = [
        for attribute in attributes do
            match attribute.Value with
            | Scalar expr -> attribute.Name, expr
            | _ -> failwith "Invalid"
    ]
    let mapValue = items |> Map.ofList
    mapValue

let rec mapRecord (kind: string option) (recordType: Type) (attributes: Attributes) =
    let ctor = FSharpValue.PreComputeRecordConstructor(recordType)
    let fields = FSharpType.GetRecordFields(recordType)
    let fieldRequiredAndValue = fields |> Array.map getRequiredAndDefaultValue

    let fieldInfo (pi: PropertyInfo) =
        let isKind = pi.GetCustomAttribute<KindAttribute>() <> null
        let isAny = pi.GetCustomAttribute<AnyAttribute>() <> null
        let name = pi.GetCustomAttribute<NameAttribute>() |> Option.ofObj |> Option.map (fun x -> x.Name)
        isKind, isAny, name

    let fieldIndices =
        fields
        |> Seq.mapi (fun idx pi  -> 
            match fieldInfo pi with
            | true, _, _ -> "__kind__", idx
            | _, true, _ -> "__any__", idx
            | _, _, Some name -> name, idx
            | _ -> failwith $"Invalid property {pi.Name}")
        |> Map

    match kind, fieldIndices |> Map.tryFind "__kind__" with
    | Some kind, Some idx -> fieldRequiredAndValue[idx] <- false, kind
    | Some _, None -> failwith "Unexpected kind"
    | None, None -> ()
    | _ -> failwith $"Expecting Kind on {recordType.Name}"

    for block in attributes do
        let index, value =
            match fieldIndices |> Map.tryFind block.Name with
            | None -> failwith $"Can't map structure to property"
            | Some index ->
                let value: obj =
                    match fields[index].PropertyType |> TypeHelpers.getKind with
                    | TypeHelpers.TypeKind.FsOption ->
                        match block.Value with
                        | Scalar expr -> Some expr
                        | Array exprs -> Some exprs
                        | Block block -> Some (mapRecord block.Kind (fields[index].PropertyType.GetGenericArguments()[0]) block.Attributes)
                    | TypeHelpers.TypeKind.FsList ->
                        if fields[index].PropertyType.GetGenericArguments()[0] = typeof<AST.Attribute> then
                            let tail = block.Value
                            let head = fieldRequiredAndValue[index] |> snd
                            addList (fields[index].PropertyType.GetGenericArguments()[0]) head tail                            
                        else
                            match block.Value with
                            | Array exprs ->
                                if fieldRequiredAndValue[index] |> fst then failwith "Already set"
                                exprs
                            | Block block ->
                                if block.Alias <> None || block.Kind <> None then failwith "Unexpected block in list"
                                let tail = mapRecord block.Kind (fields[index].PropertyType.GetGenericArguments()[0]) block.Attributes
                                let head = fieldRequiredAndValue[index] |> snd
                                addList (fields[index].PropertyType.GetGenericArguments()[0]) head tail
                            | _ -> failwith $"Can't map value to property {fields[index].Name}"
                    | TypeHelpers.TypeKind.FsRecord ->
                        match block.Value with
                        | Block block -> mapRecord block.Kind fields[index].PropertyType block.Attributes
                        | _ -> failwith $"Can't map value to property {fields[index].Name}"
                    | TypeHelpers.TypeKind.FsMap ->
                        match block.Value with
                        | Block block ->
                            match block.Alias with
                            | Some alias ->
                                let tail = mapRecord block.Kind (fields[index].PropertyType.GetGenericArguments()[1]) block.Attributes
                                let head = fieldRequiredAndValue[index] |> snd
                                addMap (fields[index].PropertyType.GetGenericArguments()[1]) head alias tail
                            | None -> mapMap block.Attributes
                        | _ -> failwith $"Can't map value to property {fields[index].Name}"
                    | _ ->
                        match block.Value with
                        | Scalar expr -> expr
                        | Array exprs -> exprs
                        | _ -> failwith $"Can't map structure to property {fields[index].Name}"
                index, value
        fieldRequiredAndValue[index] <- false, value

    let fieldValues =
        fieldRequiredAndValue
        |> Array.mapi (fun idx (required, value) -> if required then failwith $"Missing required field {recordType.Name}::{fields[idx].Name}"
                                                    else value)
    let value = ctor fieldValues
    value
