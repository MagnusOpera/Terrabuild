module Dotnet
type Dummy = interface end


open Terrabuild.Extensibility
open System.Xml.Linq
open System.IO


#nowarn "0077" // op_Explicit

let private NsNone = XNamespace.None
let private NsMsBuild = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003")

let inline (!>) (x : ^a) : ^b = (((^a or ^b) : (static member op_Explicit : ^a -> ^b) x))

let private knownProjectExtensions =
    [ "*.pssproj"
      "*.csproj"
      "*.vbproj"
      "*.fsproj"
      "*.sqlproj" ]

let findProjectFile (directory: string) =
    let projects =
        knownProjectExtensions
        |> Seq.collect (fun ext -> System.IO.Directory.EnumerateFiles(directory, ext))
    projects |> Seq.exactlyOne |> Path.GetFileName

let private parseDotnetDependencies (projectFile: string) =
    let xdoc = XDocument.Load (projectFile)
    let refs =
        xdoc.Descendants() 
        |> Seq.filter (fun x -> x.Name.LocalName = "ProjectReference")
        |> Seq.map (fun x -> !> x.Attribute(NsNone + "Include") : string)
        |> Seq.map (fun x -> x.Replace("\\", "/"))
        |> Seq.map Path.GetDirectoryName
        |> Seq.distinct
        |> List.ofSeq
    refs 



let private buildCmdLine cmd args cache =
    { Action.Command = cmd
      Action.Arguments = args
      Action.Cache = cache }


let __init__ (context: InitContext) =
    let projectFile = findProjectFile context.Directory
    let dependencies = Path.Combine(context.Directory, projectFile) |> parseDotnetDependencies 
    let properties = Map [ "projectfile", projectFile ]
    let projectInfo = { ProjectInfo.Properties = properties
                        ProjectInfo.Ignores = Set []
                        ProjectInfo.Outputs = Set [ "bin"; "obj" ]
                        ProjectInfo.Dependencies = set dependencies }
    projectInfo

let build (context: ActionContext) (configuration: string option) =
    let projectFile = context.Properties["projectfile"]
    let configuration = configuration |> Option.defaultValue "Debug"

    [ buildCmdLine "dotnet" $"restore {projectFile} --no-dependencies" Cacheability.Local
      buildCmdLine "dotnet" $"build {projectFile} -m:1 --no-dependencies --no-restore --configuration {configuration}" Cacheability.Always ]

let exec (context: ActionContext) (command: string) (arguments: string option) =
    let arguments = arguments |> Option.defaultValue ""
    [ buildCmdLine command arguments Cacheability.Always ]

let pack (context: ActionContext) (configuration: string option) (version: string option) =
    let projectFile = context.Properties["projectfile"]
    let configuration = configuration |> Option.defaultValue "Debug"
    let version = version |> Option.defaultValue "0.0.0"
    // TargetsForTfmSpecificContentInPackage ==> https://github.com/dotnet/fsharp/issues/12320
    [ buildCmdLine "dotnet" $"pack {projectFile} --no-restore --no-build --configuration {configuration} /p:Version={version} /p:TargetsForTfmSpecificContentInPackage=" Cacheability.Always ]

let publish (context: ActionContext) (configuration: string option) (runtime: string option) (trim: bool option) (single: bool option) =
    let projectFile = context.Properties["projectfile"]
    let configuration = configuration |> Option.defaultValue "Debug"

    let runtime =
        match runtime with
        | Some identifier -> $" -r {identifier}"
        | _ -> " --no-restore --no-build"
    let trim =
        match trim with
        | Some true -> " -p:PublishTrimmed=true"
        | _ -> ""
    let single =
        match single with
        | Some true -> " --self-contained"
        | _ -> ""
    [ buildCmdLine "dotnet" $"publish {projectFile} --configuration {configuration}{runtime}{trim}{single}" Cacheability.Always ]

let restore (context: ActionContext) =
    let projectFile = context.Properties["projectfile"]

    [ buildCmdLine "dotnet" $"restore {projectFile} --no-dependencies" Cacheability.Local ]

let test (context: ActionContext) (configuration: string option) (filter: string option) =
    let projectFile = context.Properties["projectfile"]
    let configuration = configuration |> Option.defaultValue "Debug"

    let filter = filter |> Option.defaultValue "true"
    [ buildCmdLine "dotnet" $"test --no-build --configuration {configuration} {projectFile} --filter \"{filter}\"" Cacheability.Always ]
