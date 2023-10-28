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



type DotnetRestore() =
    inherit StepParameters()


type DotnetBuild() =
    inherit StepParameters()
    member val Configuration = "Debug" with get, set

type DotnetTest() =
    inherit StepParameters()
    member val Configuration = "Debug" with get, set
    member val Filter = "" with get, set

type DotnetPublish() =
    inherit StepParameters()
    member val Configuration = "Debug" with get, set


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
        { Extensions.CommandLine.Command = cmd
          Extensions.CommandLine.Arguments = args }

    override _.Container = Some "mcr.microsoft.com/dotnet/sdk"

    override _.Dependencies = parseDotnetDependencies 

    override _.Outputs = [ "bin"; "obj" ]

    override _.Ignores = []

    override _.GetStepParameters action =
        match action with
        | "restore" -> typeof<DotnetRestore>
        | "build" -> typeof<DotnetBuild>
        | "test" -> typeof<DotnetTest>
        | "publish" -> typeof<DotnetPublish>
        | _ -> ArgumentException($"Unknown action {action}") |> raise

    override _.BuildStepCommands (_, parameters) =
        match parameters with
        | :? DotnetRestore as parameters ->
            [ buildCmdLine "dotnet" $"restore {projectFile} --no-dependencies" ]
        | :? DotnetBuild as parameters ->
            [ buildCmdLine "dotnet" $"restore {projectFile} --no-dependencies"
              buildCmdLine "dotnet" $"build {projectFile} -m:1 --no-dependencies --no-restore --configuration {parameters.Configuration}" ]
        | :? DotnetTest as parameters ->
            [ buildCmdLine "dotnet" $"test --no-build --configuration {parameters.Configuration} {projectFile} --filter \"{parameters.Filter}\"" ]
        | :? DotnetPublish as parameters ->
            [ buildCmdLine "dotnet" $"publish {projectFile} --no-build --configuration {parameters.Configuration}" ]
        | _ -> ArgumentException($"Unknown action") |> raise
