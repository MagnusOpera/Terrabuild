module Mapper
open System
open AST
open System.Reflection
open Microsoft.FSharp.Reflection

[<AttributeUsage(AttributeTargets.Property)>]
type Block(name: string) =
    inherit System.Attribute()

[<AttributeUsage(AttributeTargets.Property)>]
type BlockType() =
    inherit System.Attribute()

[<AttributeUsage(AttributeTargets.Property)>]
type BlockName() =
    inherit System.Attribute()

[<AttributeUsage(AttributeTargets.Property)>]
type AttributeName(name: string) =
    inherit System.Attribute()

[<AttributeUsage(AttributeTargets.Property)>]
type Required() =
    inherit System.Attribute()




let defaultValues (ty: Type): obj =
    match TypeHelpers.getKind ty with
    | TypeHelpers.TypeKind.FsList -> List.empty
    | TypeHelpers.TypeKind.FsOption -> None
    | _ -> null


let isRequired (propInfo: PropertyInfo) =
    match TypeHelpers.getKind propInfo.PropertyType with
    | TypeHelpers.TypeKind.FsOption -> false
    | TypeHelpers.TypeKind.FsList ->
        propInfo.
    | _ -> true, null



let mapBlock (block: AST.Block) (ty: Type) =
    let ctor = FSharpValue.PreComputeRecordConstructor(ty)
    let fields = FSharpType.GetRecordFields(ty)

    match block.Header with
    | Block resource -> resource
    | BlockName (resource, _) -> resource
    | BlockTypeName (resource, _, _) -> resource


let map (blocks: AST.Blocks) (ty: Type) =
    let ctor = FSharpValue.PreComputeRecordConstructor(ty)
    let fields = FSharpType.GetRecordFields(ty)

    // let fieldRequired =
    //     fields
    //     |> Array.map isRequired

    // let fieldValues =
    //     fields
    //     |> Array.map (fun info -> serializer.Default(info.PropertyType))

    let fieldIndices =
        fields
        |> Seq.mapi (fun idx pi  -> pi.Name.ToLowerInvariant(), idx)
        |> Map

    for block in blocks do
        let resourceName =
            match block.Header with
            | Block resource -> resource
            | BlockName (resource, _) -> resource
            | BlockTypeName (resource, _, _) -> resource

        let index =
            match fieldIndices |> Map.tryFind resourceName with
            | Some index -> index
            | _ -> failwith $"Unknown field {resourceName}"

        let fieldType = fields[index]
        let value = mapField block fiedType




let map<'t> (blocks: Blocks) =
    let recordType = typeof<'t>
    map blocks recordType
