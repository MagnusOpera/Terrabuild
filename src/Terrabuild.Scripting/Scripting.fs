module Terrabuild.Scripting
open System.Reflection
open FSharp.Compiler.CodeAnalysis
open System.IO
open FSharp.Compiler.Diagnostics
open Terrabuild.Expressions
open System
open Microsoft.FSharp.Reflection

let checker = FSharpChecker.Create()
let mutable cache = Map.empty

type Invocable(method: MethodInfo) =

    let convertToNone (prmType: Type) =
        if prmType.GetGenericTypeDefinition() = typedefof<Option<_>> then
            let template = typedefof<Option<_>>
            let genType = template.MakeGenericType([| prmType.GenericTypeArguments[0] |])
            let cases = FSharp.Reflection.FSharpType.GetUnionCases(genType)
            let noneCase = cases |> Array.find (fun case -> case.Name = "None")
            FSharp.Reflection.FSharpValue.MakeUnion(noneCase, [| |])
        else
            failwith $"Unknown parameter type"

    let convertToSome (prmType: Type) (value: obj) =
        if prmType.GetGenericTypeDefinition() = typedefof<Option<_>> then
            let template = typedefof<Option<_>>
            let genType = template.MakeGenericType([| prmType.GenericTypeArguments[0] |])
            let cases = FSharp.Reflection.FSharpType.GetUnionCases(genType)
            let someCase = cases |> Array.find (fun case -> case.Name = "Some")
            FSharp.Reflection.FSharpValue.MakeUnion(someCase, [| value |])
        else
            failwith $"Unknown parameter type"


    let rec mapParameter (value: Value) (name: string) (prmType: Type): obj =
        match value with
        | Value.Nothing ->
            if prmType.IsGenericType then convertToNone prmType
            else failwith $"Can't assign default value to parameter {name}"

        | Value.Bool value ->
            if value.GetType().IsAssignableTo(prmType) then value
            elif prmType.IsGenericType then convertToSome prmType value
            else failwith $"Can't assign default value to parameter {name}"

        | Value.String value ->
            if value.GetType().IsAssignableTo(prmType) then value
            elif prmType.IsGenericType then convertToSome prmType value
            else failwith $"Can't assign default value to parameter {name}"

        | Value.Map map ->
            match TypeHelpers.getKind prmType with
            | TypeHelpers.TypeKind.FsRecord ->
                let ctor = FSharpValue.PreComputeRecordConstructor(prmType)
                let fields = FSharpType.GetRecordFields(prmType)
                let fieldIndices = fields |> Array.mapi (fun index prm -> prm.Name, index) |> Map
                let fieldValues = Array.create (fields.Length) (false, null)
                for (KeyValue (key, value)) in map do
                    match fieldIndices |> Map.tryFind key with
                    | None -> failwith $"Property {key} does not exists"
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
            | _ -> failwith $"Can't assign default value to parameter {name}"

    let mapParameters (map: Map<string, Value>) (prms: ParameterInfo array) =
        prms
        |> Array.map (fun prm ->
            match map |> Map.tryFind prm.Name with
            | None -> mapParameter Value.Nothing prm.Name prm.ParameterType
            | Some value -> mapParameter value prm.Name prm.ParameterType) 

    member _.BuildArgs (value: Value) =
        match value with
        | Value.Map map -> 
            let prms = method.GetParameters()
            mapParameters map prms |> Ok
        | _ -> Error $"Expecting a map for build arguments"

    member _.Invoke (args: obj array) =
        method.Invoke(null, args)

type Script(assembly: Assembly) =
    let mainType = assembly.GetType("Script")

    member _.GetMethod(name: string) =
        let method = mainType.GetMethod(name)
        match method with
        | null -> Error $"Function {name} is not defined"
        | method -> Invocable(method) |> Ok


let loadScript (scriptFile) =
    let scriptFile = Path.GetFullPath(scriptFile)
    match cache |> Map.tryFind scriptFile with
    | Some script -> Ok script
    | _ ->
        try
            let outputDllName = $"{Path.GetTempFileName()}.dll"

            let compilerArgs = [|
                "-a"; scriptFile
                "--targetprofile:netcore"
                "--target:library"
                $"--out:{outputDllName}"
            |]

            let errors, _ = checker.Compile(compilerArgs) |> Async.RunSynchronously
            let firstError = errors |> Array.tryFind (fun x -> x.Severity = FSharpDiagnosticSeverity.Error)
            if firstError <> None then failwithf $"Error while compiling script {scriptFile}: {firstError.Value}"

            let assembly = Assembly.LoadFile outputDllName
            let script = Script(assembly)
            cache <- cache |> Map.add scriptFile script
            Ok script
        with
            exn -> Error exn
