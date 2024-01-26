namespace Terrabuild.Dotnet
open Extensions

#nowarn "0077" // op_Explicit

module Xml =
    open System.Xml.Linq
    let NsNone = XNamespace.None
    let NsMsBuild = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003")

    let inline (!>) (x : ^a) : ^b = (((^a or ^b) : (static member op_Explicit : ^a -> ^b) x))

open System
open System.Xml.Linq
open Xml
open System.IO

type Builder(context: Context) =
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


    interface IBuilder with
        member _.Container = Some "mcr.microsoft.com/dotnet/sdk:8.0"

        member _.Dependencies = parseDotnetDependencies 

        member _.Outputs = [ "bin"; "obj" ]

        member _.Ignores = []

        member _.CreateCommand(action: string): ICommandFactory = 
            match action with
            | "restore" -> Restore.Command(projectFile)
            | "build" -> Build.Command(projectFile)
            | "test" -> Test.Command(projectFile)
            | "publish" -> Publish.Command(projectFile)
            | "pack" -> Pack.Command(projectFile)
            | "exec" -> Exec.Command()
            | _ -> ArgumentException($"Unknown action {action}") |> raise
