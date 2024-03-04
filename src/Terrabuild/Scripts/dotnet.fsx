module Script

#if !TERRABUILD_SCRIPT
#r "../bin/Debug/net8.0/Terrabuild.Extensibility.dll"
#endif


open Terrabuild.Extensibility
open System.Xml.Linq
open System.IO
    open System.Xml.Linq



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


type Globals = {
    ProjectFile: string
    Configuration: string
}





let mutable globals = None


let init (directory: string) (projectFile: string option) (configuration: string option) =
    printfn $"Running dotnet::init"
    let projectFile = projectFile |> Option.defaultWith (fun () -> findProjectFile directory)
    let configuration = configuration |> Option.defaultValue "Debug"

    globals <- Some { ProjectFile = projectFile
                      Configuration = configuration }



let build () =
    printfn $"Running dotnet::build"
    let globals = globals.Value


    [ buildCmdLine "dotnet" $"restore {globals.ProjectFile} --no-dependencies" Cacheability.Local
      buildCmdLine "dotnet" $"build {globals.ProjectFile} -m:1 --no-dependencies --no-restore --configuration {globals.Configuration}" Cacheability.Always ]

let exec (command: string) (arguments: string option) =
    let arguments = arguments |> Option.defaultValue ""
    [ buildCmdLine command arguments Cacheability.Always ]

let pack (version: string option) =
    let globals = globals.Value
    let version = version |> Option.defaultValue "0.0.0"
    // TargetsForTfmSpecificContentInPackage ==> https://github.com/dotnet/fsharp/issues/12320
    [ buildCmdLine "dotnet" $"pack {globals.ProjectFile} --no-restore --no-build --configuration {globals.Configuration} /p:Version={version} /p:TargetsForTfmSpecificContentInPackage=" Cacheability.Always ]

let publish (runtime: string option) (trim: bool option) (single: bool option) =
    let globals = globals.Value

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
    [ buildCmdLine "dotnet" $"publish {globals.ProjectFile} --configuration {globals.Configuration}{runtime}{trim}{single}" Cacheability.Always ]

let restore() =
    let globals = globals.Value

    [ buildCmdLine "dotnet" $"restore {globals.ProjectFile} --no-dependencies" Cacheability.Local ]

let test (filter: string option) =
    let globals = globals.Value
    let filter = filter |> Option.defaultValue "true"
    [ buildCmdLine "dotnet" $"test --no-build --configuration {globals.Configuration} {globals.ProjectFile} --filter \"{filter}\"" Cacheability.Always ]
