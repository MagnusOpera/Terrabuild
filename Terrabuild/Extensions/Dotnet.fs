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


type DotnetBuild = {
    Configuration: string option
}

type DotnetTest = {
    Configuration: string option
    Filter: string option
}

type DotnetPublish = {
    Configuration: string option
}

type Dotnet(context) =
    inherit Extension(context)

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
        let refs = xdoc.Descendants() 
                        |> Seq.filter (fun x -> x.Name.LocalName = "ProjectReference")
                        |> Seq.map (fun x -> !> x.Attribute(NsNone + "Include") : string)
                        |> Seq.map (fun x -> x.Replace("\\", "/"))
                        |> Seq.map Path.GetDirectoryName
                        |> Seq.distinct
                        |> List.ofSeq
        refs 

    let buildCmdLine cmd args =
        { CommandLine.Command = cmd
          CommandLine.Arguments = args
          CommandLine.Cache = Cacheability.Always }

    override _.Container = Some "mcr.microsoft.com/dotnet/sdk:8.0"

    override _.Dependencies = parseDotnetDependencies 

    override _.Outputs = [ "bin"; "obj" ]

    override _.Ignores = []

    override _.GetStepParameters action =
        match action with
        | "restore" -> None
        | "build" -> Some typeof<DotnetBuild>
        | "test" -> Some typeof<DotnetTest>
        | "publish" -> Some typeof<DotnetPublish>
        | _ -> ArgumentException($"Unknown action {action}") |> raise

    override _.BuildStepCommands (action, parameters) =
        match parameters, action with
        | _, "restore" ->
            [ buildCmdLine "dotnet" $"restore {projectFile} --no-dependencies" ]
        | :? DotnetBuild as parameters, _ ->
            let config = parameters.Configuration |> Option.defaultValue "Debug"
            [ buildCmdLine "dotnet" $"restore {projectFile} --no-dependencies"
              buildCmdLine "dotnet" $"build {projectFile} -m:1 --no-dependencies --no-restore --configuration {config}" ]
        | :? DotnetTest as parameters, _ ->
            let config = parameters.Configuration |> Option.defaultValue "Debug"
            let filter = parameters.Filter |> Option.defaultValue "true"
            [ buildCmdLine "dotnet" $"test --no-build --configuration {config} {projectFile} --filter \"{filter}\"" ]
        | :? DotnetPublish as parameters, _ ->
            let config = parameters.Configuration |> Option.defaultValue "Debug"
            [ buildCmdLine "dotnet" $"publish {projectFile} --no-build --configuration {config}" ]
        | _ -> ArgumentException($"Unknown action") |> raise
