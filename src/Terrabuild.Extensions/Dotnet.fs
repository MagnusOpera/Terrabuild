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

    [<Literal>]
    let defaultConfiguration = "Debug"


/// <summary>
/// Add support for .net projects.
/// </summary>

type Dotnet() =

    /// <summary>
    /// Provides default values for project.
    /// </summary>
    /// <param name="ignores" example="[ &quot;**/*.binlog&quot; ]">Default values.</param>
    /// <param name="outputs" example="[ &quot;bin/&quot; &quot;obj/&quot; &quot;**/*.binlog&quot;]">Default values.</param>
    /// <param name="dependencies" example="[ &lt;ProjectReference /&gt; from project ]">Default values.</param>
    static member __init__ (context: InitContext) =
        let projectFile = DotnetHelpers.findProjectFile context.Directory
        let dependencies = Path.Combine(context.Directory, projectFile) |> DotnetHelpers.parseDotnetDependencies 
        let projectInfo =
            { ProjectInfo.Default
              with Ignores = Set [ "**/*.binlog" ]
                   Outputs = Set [ "bin/"; "obj/"; "**/*.binlog" ]
                   Dependencies = set dependencies }
        projectInfo

    /// <summary title="Build project.">
    /// Build project and ensure packages are available first.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration to use to build project. Default is `Debug`.</param>
    /// <param name="projectfile" example="&quot;project.fsproj&quot;">Force usage of project file for build.</param>
    /// <param name="log" example="true">Enable binlog for the build.</param>
    static member build (configuration: string option) (projectfile: string option) (log: bool option)=
        let projectfile = projectfile |> Option.defaultValue ""
        let configuration = configuration |> Option.defaultValue DotnetHelpers.defaultConfiguration
        let logger =
            match log with
            | Some true -> " -bl"
            | _ -> ""

        scope Cacheability.Always
        |> andThen "dotnet" $"restore {projectfile} --no-dependencies" 
        |> andThen "dotnet" $"build {projectfile} -m:1 --no-dependencies --no-restore --configuration {configuration}{logger}"

    /// <summary>
    /// Run a dotnet command.
    /// </summary>
    /// <param name="command" example="&quot;format&quot;">Command to execute.</param>
    /// <param name="arguments" example="&quot;--verify-no-changes&quot;">Arguments for command.</param>
    static member exec (command: string) (arguments: string option) =
        let arguments = arguments |> Option.defaultValue ""
        scope Cacheability.Always
        |> andThen command arguments


    /// <summary>
    /// Pack a project.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration for pack command.</param>
    /// <param name="projectfile" example="&quot;project.fsproj&quot;">Force usage of project file for build.</param>
    /// <param name="version" example="&quot;1.0.0&quot;">Version for pack command.</param>
    static member pack (configuration: string option) (projectfile: string option) (version: string option) =
        let projectfile = projectfile |> Option.defaultValue ""
        let configuration = configuration |> Option.defaultValue DotnetHelpers.defaultConfiguration
        let version = version |> Option.defaultValue "0.0.0"

        // NOTE for TargetsForTfmSpecificContentInPackage: https://github.com/dotnet/fsharp/issues/12320
        scope Cacheability.Always
        |> andThen "dotnet" $"pack {projectfile} --no-restore --no-build --configuration {configuration} /p:Version={version} /p:TargetsForTfmSpecificContentInPackage="

    /// <summary>
    /// Publish a project.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration for publish command.</param>
    /// <param name="projectfile" example="&quot;project.fsproj&quot;">Force usage of project file for publish.</param>
    /// <param name="runtime" example="&quot;linux-x64&quot;">Runtime for publish.</param>
    /// <param name="trim" example="true">Instruct to trim published project.</param>
    /// <param name="single" example="true">Instruct to publish project as self-contained.</param>
    static member publish (configuration: string option) (projectfile: string option) (runtime: string option) (trim: bool option) (single: bool option) =
        let projectfile = projectfile |> Option.defaultValue ""
        let configuration = configuration |> Option.defaultValue DotnetHelpers.defaultConfiguration

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
        scope Cacheability.Always
        |> andThen "dotnet" $"restore {projectfile} --no-dependencies" 
        |> andThen "dotnet" $"publish {projectfile} --configuration {configuration}{runtime}{trim}{single}"

    /// <summary>
    /// Restore packages.
    /// </summary>
    /// <param name="projectfile" example="&quot;project.fsproj&quot;">Force usage of project file for publish.</param>
    static member restore (projectfile: string option) =
        let projectfile = projectfile |> Option.defaultValue ""
        scope Cacheability.Local
        |> andThen "dotnet" $"restore {projectfile} --no-dependencies"


    /// <summary>
    /// Test project.
    /// </summary>
    /// <param name="configuration" example="&quot;Release&quot;">Configuration for publish command.</param>
    /// <param name="projectfile" example="&quot;project.fsproj&quot;">Force usage of project file for publish.</param>
    static member test (configuration: string option) (projectfile: string option) (filter: string option) =
        let projectfile = projectfile |> Option.defaultValue ""
        let configuration = configuration |> Option.defaultValue DotnetHelpers.defaultConfiguration

        let filter = filter |> Option.map (fun filter -> $" --filter \"{filter}\"") |> Option.defaultValue ""
        scope Cacheability.Always
        |> andThen "dotnet" $"test --no-build --configuration {configuration} {filter} {projectfile}"
