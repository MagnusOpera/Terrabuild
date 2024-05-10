module Terrabuild.Scripting
open System.Reflection
open FSharp.Compiler.CodeAnalysis
open System.IO
open FSharp.Compiler.Diagnostics
open Terrabuild.Expressions
open System
open Microsoft.FSharp.Reflection
open Errors

let checker = FSharpChecker.Create()
let mutable cache = Map.empty



type Invocable(method: MethodInfo) =

    let convertToNone (prmType: Type) =
        let template = typedefof<Option<_>>
        let genType = template.MakeGenericType([| prmType.GenericTypeArguments[0] |])
        let cases = FSharp.Reflection.FSharpType.GetUnionCases(genType)
        let noneCase = cases |> Array.find (fun case -> case.Name = "None")
        FSharp.Reflection.FSharpValue.MakeUnion(noneCase, [| |])

    let convertToSome (prmType: Type) (value: obj) =
        let template = typedefof<Option<_>>
        let genType = template.MakeGenericType([| prmType.GenericTypeArguments[0] |])
        let cases = FSharp.Reflection.FSharpType.GetUnionCases(genType)
        let someCase = cases |> Array.find (fun case -> case.Name = "Some")
        FSharp.Reflection.FSharpValue.MakeUnion(someCase, [| value |])

    let rec mapParameter (value: Value) (name: string) (prmType: Type): obj =
        match value with
        | Value.Nothing ->
            if prmType.IsGenericType && prmType.GetGenericTypeDefinition() = typedefof<Option<_>> then convertToNone prmType
            elif prmType.IsGenericType && prmType = typeof<Map<string, string>> then
                let emptyMap : Map<string, string> = Map.empty
                emptyMap
            else TerrabuildException.Raise($"Can't assign default value to parameter '{name}'")

        | Value.Bool value ->
            if value.GetType().IsAssignableTo(prmType) then value
            elif prmType.IsGenericType && prmType.GetGenericTypeDefinition() = typedefof<Option<_>> then convertToSome prmType value
            else TerrabuildException.Raise($"Can't assign default value to parameter '{name}'")

        | Value.String value ->
            if value.GetType().IsAssignableTo(prmType) then value
            elif prmType.IsGenericType && prmType.GetGenericTypeDefinition() = typedefof<Option<_>> then convertToSome prmType value
            else TerrabuildException.Raise($"Can't assign default value to parameter '{name}'")

        | Value.Number value ->
            if value.GetType().IsAssignableTo(prmType) then value
            elif prmType.IsGenericType && prmType.GetGenericTypeDefinition() = typedefof<Option<_>> then convertToSome prmType value
            else TerrabuildException.Raise($"Can't assign default value to parameter '{name}'")

        | Value.Object obj -> obj

        | Value.Map map ->
            match TypeHelpers.getKind prmType with
            | TypeHelpers.TypeKind.FsRecord ->
                let ctor = FSharpValue.PreComputeRecordConstructor(prmType)
                let fields = FSharpType.GetRecordFields(prmType)
                let fieldIndices = fields |> Array.mapi (fun index prm -> prm.Name, index) |> Map
                let fieldValues = Array.create (fields.Length) (false, null)
                for (KeyValue (key, value)) in map do
                    match fieldIndices |> Map.tryFind key with
                    | None -> TerrabuildException.Raise($"Property {key} does not exists")
                    | Some idx -> 
                        let field = fields[idx]
                        let value = mapParameter value field.Name field.PropertyType
                        fieldValues[idx] <- true, value
                let ctorValues =
                    fieldValues
                    |> Array.mapi (fun idx (initialized, value) -> 
                        if initialized then value
                        else
                            let field = fields[idx]
                            let value = mapParameter Value.Nothing field.Name field.PropertyType
                            value) 
                ctor(ctorValues)
            | TypeHelpers.TypeKind.FsMap ->
                let values = map |> Map.map (fun name value -> mapParameter value name typeof<string> :?> string)
                values
            | _ -> TerrabuildException.Raise($"Can't assign default value to parameter '{name}'")

    let mapParameters (map: Map<string, Value>) (prms: ParameterInfo array) =
        prms
        |> Array.map (fun prm ->
            match map |> Map.tryFind prm.Name with
            | None -> mapParameter Value.Nothing prm.Name prm.ParameterType
            | Some value -> mapParameter value prm.Name prm.ParameterType) 

    let buildArgs (value: Value) =
        match value with
        | Value.Map map -> 
            let prms = method.GetParameters()
            mapParameters map prms
        | _ -> TerrabuildException.Raise($"Expecting a map for build arguments")

    let invoke args =
        method.Invoke(null, args)

    member _.Invoke<'t> (value: Value) =
        let mapping = buildArgs value
        invoke mapping :?> 't

type Script(mainType: Type) =
    member _.GetMethod(name: string) =
        match mainType.GetMethod(name, BindingFlags.IgnoreCase ||| BindingFlags.Public ||| BindingFlags.Static) with
        | null -> None
        | mi -> Invocable(mi) |> Some

let loadScript (references: string list) (scriptFile) =
    let scriptFile = Path.GetFullPath(scriptFile)
    match cache |> Map.tryFind scriptFile with
    | Some script -> script
    | _ ->
        let outputDllName = $"{Path.GetTempFileName()}.dll"

        let compilerArgs = [|
            "-a"; scriptFile
            "--targetprofile:netcore"
            "--target:library"
            $"--out:{outputDllName}"
            "--define:TERRABUILD_SCRIPT"
            for reference in references do $"--reference:{reference}"
        |]

        let errors, _ = checker.Compile(compilerArgs) |> Async.RunSynchronously
        let firstError = errors |> Array.tryFind (fun x -> x.Severity = FSharpDiagnosticSeverity.Error)
        if firstError <> None then TerrabuildException.Raise($"Error while compiling script {scriptFile}: {firstError.Value}")

        let assembly = Assembly.LoadFile outputDllName
        let expectedMainTypeName = Path.GetFileNameWithoutExtension(scriptFile)
        let mainType = 
            match assembly.GetTypes() |> Seq.tryFind (fun t -> String.Compare(t.Name, expectedMainTypeName, true) = 0) with
            | Some mainType -> mainType
            | _ -> TerrabuildException.Raise($"Failed to identify function scope (either module or root class '{expectedMainTypeName}')")

        let script = Script(mainType)
        cache <- cache |> Map.add scriptFile script
        script
