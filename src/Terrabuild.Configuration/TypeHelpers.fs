module private TypeHelpers
open System
open System.Reflection
open System.Collections.Generic
open FSharp.Reflection

type TypeKind =
    | FsRecord = 0
    | FsUnion = 1
    | FsList = 2
    | FsSet = 3
    | FsMap = 4
    | FsTuple = 5
    | FsUnit = 6
    | FsOption = 7
    | FsVOption = 8
    | List = 50
    | Array = 51
    | Dictionary = 52
    | Nullable = 53
    | Other = 200

let private fslistTy = typedefof<_ list>
let private fssetTy = typedefof<Set<_>>
let private fsmapTy = typedefof<Map<_, _>>
let private listTy = typedefof<List<_>>
let private dictionaryTy = typedefof<Dictionary<_, _>>
let private nullableTy = typedefof<Nullable<_>>
let private fsunit = typeof<unit>
let private fsoptionTy = typedefof<option<_>>
let private fsvoptionTy = typedefof<voption<_>>

let private matchType (ty: Type) =
    if ty.IsGenericType && ty.GetGenericTypeDefinition() = fslistTy then TypeKind.FsList
    elif ty.IsGenericType && ty.GetGenericTypeDefinition() = fssetTy then TypeKind.FsSet
    elif ty.IsGenericType && ty.GetGenericTypeDefinition() = fsmapTy then TypeKind.FsMap
    elif ty.IsGenericType && ty.GetGenericTypeDefinition() = listTy then TypeKind.List
    elif ty.IsGenericType && ty.GetGenericTypeDefinition() = dictionaryTy then TypeKind.Dictionary
    elif ty.IsGenericType && ty.GetGenericTypeDefinition() = nullableTy then TypeKind.Nullable
    elif FSharpType.IsTuple(ty) then TypeKind.FsTuple
    elif FSharpType.IsUnion(ty, true) then
        if ty.IsGenericType then
            let gen = ty.GetGenericTypeDefinition()
            if gen = fsoptionTy then TypeKind.FsOption
            elif gen = fsvoptionTy then TypeKind.FsVOption
            else TypeKind.FsUnion
        else
            TypeKind.FsUnion
    elif FSharpType.IsRecord(ty, true) then TypeKind.FsRecord
    elif ty = fsunit then TypeKind.FsUnit
    elif ty.IsArray then TypeKind.Array
    else TypeKind.Other

let private readMethod (ty: Type) = ty.GetMethod("Read")
let private defaultMethod (ty: Type) = ty.GetMethod("Default")

let private cache = System.Collections.Concurrent.ConcurrentDictionary<Type, TypeKind>()
let getKind ty = cache.GetOrAdd(ty, matchType)

let private readCache = System.Collections.Concurrent.ConcurrentDictionary<Type, MethodInfo>()
let getRead ty = readCache.GetOrAdd(ty, readMethod)

let private defaultCache = System.Collections.Concurrent.ConcurrentDictionary<Type, MethodInfo>()
let getDefault ty = defaultCache.GetOrAdd(ty, defaultMethod)


let nrtContext = NullabilityInfoContext()
let getRequired noneIsEmpty (ty: Type) (nrtInfo: NullabilityInfo) _ : bool =
    match nrtInfo.ReadState with
    | NullabilityState.Nullable -> false
    | NullabilityState.NotNull -> true
    | _ ->
        // F# type ?
        match ty.GetCustomAttribute(typeof<CompilationMappingAttribute>) with
        | null ->
            match getKind ty with
            // provide some nullability for few BCL types
            | TypeKind.Nullable ->
                false
            // nullability on collection depends on NRT
            | TypeKind.List
            | TypeKind.Dictionary
            | TypeKind.Array ->
                noneIsEmpty |> not
            // better sad than sorry: no clues about nullability and not F# so in doubt force required
            | _ ->
                true
        | _ ->
            // F# null allowed ?
            match ty.GetCustomAttribute(typeof<AllowNullLiteralAttribute>) with
            | null ->
                match getKind ty with
                // provide some nullability for few F# types
                | TypeKind.FsUnit
                | TypeKind.FsOption
                | TypeKind.FsVOption ->
                    false
                // nullability on collection depends on NRT
                | TypeKind.FsList
                | TypeKind.FsSet
                | TypeKind.FsMap ->
                    noneIsEmpty |> not
                // all other F# types are mandatory
                | _ ->
                    true
            | _ ->
                false

let private propInfoRequiredCache = System.Collections.Concurrent.ConcurrentDictionary<PropertyInfo, bool>()
let getPropertyRequired noneIsEmpty (propInfo: PropertyInfo) = propInfoRequiredCache.GetOrAdd(propInfo, getRequired noneIsEmpty propInfo.PropertyType (nrtContext.Create(propInfo)))

let private paramInfoRequiredCache = System.Collections.Concurrent.ConcurrentDictionary<ParameterInfo, bool>()
let getParameterRequired noneIsEmpty (paramInfo: ParameterInfo) = paramInfoRequiredCache.GetOrAdd(paramInfo, getRequired noneIsEmpty paramInfo.ParameterType (nrtContext.Create(paramInfo)))
