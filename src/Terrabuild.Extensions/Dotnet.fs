namespace Terrabuild.Extensions


open Terrabuild.Extensibility
open System.Xml.Linq
open System.IO


#nowarn "0077" // op_Explicit

module DotnetHelpers =

    let private NsNone = XNamespace.None

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

    let parseDotnetDependencies (projectFile: string) =
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


type Dotnet() =
    static member __init__ (context: InitContext) =
        let projectFile = DotnetHelpers.findProjectFile context.Directory
        let dependencies = Path.Combine(context.Directory, projectFile) |> DotnetHelpers.parseDotnetDependencies 
        let properties = Map [ "projectfile", projectFile ]
        let projectInfo = { ProjectInfo.Properties = properties
                            ProjectInfo.Ignores = Set []
                            ProjectInfo.Outputs = Set [ "bin"; "obj" ]
                            ProjectInfo.Dependencies = set dependencies }
        projectInfo


    static member Build (context: ActionContext) (configuration: string option) =
        let projectFile = context.Properties["projectfile"]
        let configuration = configuration |> Option.defaultValue "Debug"

        [ Action.Build "dotnet" $"restore {projectFile} --no-dependencies" Cacheability.Always
          Action.Build "dotnet" $"build {projectFile} -m:1 --no-dependencies --no-restore --configuration {configuration}" Cacheability.Always ]


    static member Exec (command: string) (arguments: string option) =
        let arguments = arguments |> Option.defaultValue ""
        [ Action.Build command arguments Cacheability.Always ]


    static member Pack (context: ActionContext) (configuration: string option) (version: string option) =
        let projectFile = context.Properties["projectfile"]
        let configuration = configuration |> Option.defaultValue "Debug"
        let version = version |> Option.defaultValue "0.0.0"
        // TargetsForTfmSpecificContentInPackage ==> https://github.com/dotnet/fsharp/issues/12320
        [ Action.Build "dotnet" $"pack {projectFile} --no-restore --no-build --configuration {configuration} /p:Version={version} /p:TargetsForTfmSpecificContentInPackage=" Cacheability.Always ]


    static member Publish (context: ActionContext) (configuration: string option) (runtime: string option) (trim: bool option) (single: bool option) =
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
        [ Action.Build "dotnet" $"restore {projectFile} --no-dependencies" Cacheability.Always
          Action.Build "dotnet" $"publish {projectFile} --configuration {configuration}{runtime}{trim}{single}" Cacheability.Always ]


    static member Restore (context: ActionContext) =
        let projectFile = context.Properties["projectfile"]

        [ Action.Build "dotnet" $"restore {projectFile} --no-dependencies" Cacheability.Local ]


    static member Test (context: ActionContext) (configuration: string option) (filter: string option) =
        let projectFile = context.Properties["projectfile"]
        let configuration = configuration |> Option.defaultValue "Debug"

        let filter = filter |> Option.defaultValue "true"
        [ Action.Build "dotnet" $"test --no-build --configuration {configuration} {projectFile} --filter \"{filter}\"" Cacheability.Always ]
