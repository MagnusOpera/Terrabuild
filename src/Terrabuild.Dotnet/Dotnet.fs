namespace Extensions

#nowarn "0077" // op_Explicit

module Xml =
    open System.Xml.Linq
    let NsNone = XNamespace.None
    let NsMsBuild = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003")

    let inline (!>) (x : ^a) : ^b = (((^a or ^b) : (static member op_Explicit : ^a -> ^b) x))

open System
open System.Xml.Linq
open Extensions
open Xml
open System.IO
open System.ComponentModel.Composition

type DotnetBuild = {
    Configuration: string option
}

type DotnetTest = {
    Configuration: string option
    Filter: string option
}

type DotnetPublish = {
    Configuration: string option
    Runtime: string option
    Trim: bool option
    Single: bool option
}

type DotnetPack = {
    Configuration: string option
    Version: string option
}

type DotnetExec = {
    Command: string
    Arguments: string option
}

type Dotnet(context: Context) =
    let knownProjectExtensions =
        [ "*.pssproj"
          "*.csproj"
          "*.vbproj"
          "*.fsproj"
          "*.sqlproj" ]

    let projectFile =
        match context.With with
        | Some projectFile -> projectFile
        | _ ->
            let projects =
                knownProjectExtensions
                |> Seq.collect (fun ext -> System.IO.Directory.EnumerateFiles(context.Directory, ext))
            projects |> Seq.exactlyOne |> Path.GetFileName

    let parseDotnetDependencies =
        let project = Path.Combine(context.Directory, projectFile)
        let xdoc = XDocument.Load (project)
        let refs =
            xdoc.Descendants() 
            |> Seq.filter (fun x -> x.Name.LocalName = "ProjectReference")
            |> Seq.map (fun x -> !> x.Attribute(NsNone + "Include") : string)
            |> Seq.map (fun x -> x.Replace("\\", "/"))
            |> Seq.map Path.GetDirectoryName
            |> Seq.distinct
            |> List.ofSeq
        refs 

    let buildCmdLine cmd args cache =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = cache }

    interface IExtension with
        member _.Container = Some "mcr.microsoft.com/dotnet/sdk:8.0"

        member _.Dependencies = parseDotnetDependencies 

        member _.Outputs = [ "bin"; "obj" ]

        member _.Ignores = []

        member _.GetStepParameters action =
            match action with
            | "restore" -> None
            | "build" -> Some typeof<DotnetBuild>
            | "test" -> Some typeof<DotnetTest>
            | "publish" -> Some typeof<DotnetPublish>
            | "pack" -> Some typeof<DotnetPack>
            | "exec" -> Some typeof<DotnetExec>
            | _ -> ArgumentException($"Unknown action {action}") |> raise

        member _.BuildStepCommands (action, parameters) =
            match parameters, action with
            | _, "restore" ->
                [ buildCmdLine "dotnet" $"restore {projectFile} --no-dependencies" Cacheability.Local ]
            | :? DotnetBuild as parameters, _ ->
                let config = parameters.Configuration |> Option.defaultValue "Debug"
                [ buildCmdLine "dotnet" $"restore {projectFile} --no-dependencies" Cacheability.Local
                  buildCmdLine "dotnet" $"build {projectFile} -m:1 --no-dependencies --no-restore --configuration {config}" Cacheability.Always ]
            | :? DotnetTest as parameters, _ ->
                let config = parameters.Configuration |> Option.defaultValue "Debug"
                let filter = parameters.Filter |> Option.defaultValue "true"
                [ buildCmdLine "dotnet" $"test --no-build --configuration {config} {projectFile} --filter \"{filter}\"" Cacheability.Always ]
            | :? DotnetPublish as parameters, _ ->
                let config = parameters.Configuration |> Option.defaultValue "Debug"
                let runtime =
                    match parameters.Runtime with
                    | Some identifier -> $" -r {identifier}"
                    | _ -> " --no-restore --no-build"
                let trim =
                    match parameters.Trim with
                    | Some true -> " -p:PublishTrimmed=true"
                    | _ -> ""
                let single =
                    match parameters.Single with
                    | Some true -> " --self-contained"
                    | _ -> ""
                [ buildCmdLine "dotnet" $"publish {projectFile} --configuration {config}{runtime}{trim}{single}" Cacheability.Always ]
            | :? DotnetPack as parameters, _ ->
                let config = parameters.Configuration |> Option.defaultValue "Debug"
                let version = parameters.Version |> Option.defaultValue "0.0.0"
                // TargetsForTfmSpecificContentInPackage ==> https://github.com/dotnet/fsharp/issues/12320
                [ buildCmdLine "dotnet" $"pack {projectFile} --no-restore --no-build --configuration {config} /p:Version={version} /p:TargetsForTfmSpecificContentInPackage=" Cacheability.Always ]
            | :? DotnetExec as parameters, _ ->
                let args = parameters.Arguments |> Option.defaultValue ""
                [ buildCmdLine parameters.Command args Cacheability.Always ]
            | _ -> ArgumentException($"Unknown action") |> raise


[<Export("dotnet", typeof<IExtensionFactory>)>]
type DotnetFactory() =
    interface IExtensionFactory with
        member _.Create ctx =
            Dotnet(ctx)
