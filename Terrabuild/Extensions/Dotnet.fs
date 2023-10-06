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

type Dotnet(context) =
    inherit Extension(context)

    let knownProjectExtensions =
        [ "*.pssproj"
          "*.csproj"
          "*.vbproj"
          "*.fsproj"
          "*.sqlproj" ]

    let parseDotnetDependencies =

        let projectFile =
            if context.ProjectFile |> String.IsNullOrEmpty then
                let projects =
                    knownProjectExtensions
                    |> Seq.collect (fun ext -> System.IO.Directory.EnumerateFiles(context.ProjectDirectory, ext))
                projects |> Seq.exactlyOne |> Path.GetFileName
            else
                context.ProjectFile

        let project = Path.Combine(context.ProjectDirectory, projectFile)
        let xdoc = XDocument.Load (project)
        let refs = xdoc.Descendants() 
                        |> Seq.filter (fun x -> x.Name.LocalName = "ProjectReference")
                        |> Seq.map (fun x -> !> x.Attribute(NsNone + "Include") : string)
                        |> Seq.map (fun x -> x.Replace("\\", "/"))
                        |> Seq.map Path.GetDirectoryName
                        |> Seq.distinct
                        |> List.ofSeq
        refs 

    override _.Capabilities = Capabilities.Dependencies
                              ||| Capabilities.Steps
                              ||| Capabilities.Outputs

    override _.Dependencies = parseDotnetDependencies 

    override _.Outputs = [ "bin"; "obj" ]

    override _.Ignores = NotSupportedException() |> raise

    override _.GetStep(action, args) =
        let configuration = args |> Map.tryFind "configuration" |> Option.defaultValue "Debug"
        let arguments = args |> Map.tryFind "arguments" |> Option.defaultValue ""
        let dotnetArgs = $"{action} --no-dependencies --configuration {configuration} {arguments}"
        match action with
        | "build" | "publish" | "run" | "pack" -> [ { Command = "dotnet"; Arguments = dotnetArgs } ]
        | _ -> failwith $"Unsupported action '{action}'"
