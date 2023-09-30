namespace Extensions.Dotnet

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

type DotnetExtension(context) =
    inherit Extension(context)

    let parseDotnetDependencies =
        let project = Path.Combine(context.ProjectDirectory, context.ProjectFile)
        let xdoc = XDocument.Load (project)
        let refs = xdoc.Descendants() 
                        |> Seq.filter (fun x -> x.Name.LocalName = "ProjectReference")
                        |> Seq.map (fun x -> !> x.Attribute(NsNone + "Include") : string)
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
        let arguments = args |> Map.tryFind "args" |> Option.defaultValue ""
        let dotnetArgs = $"{action} --no-dependencies --configuration {configuration} {arguments}"
        match action with
        | "build" | "publish" | "run" | "pack" -> [ { Command = "dotnet"; Arguments = dotnetArgs } ]
        | _ -> failwith $"Unsupported action '{action}'"
