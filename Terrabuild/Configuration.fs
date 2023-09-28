module Configuration
open System.IO
open System.Xml.Linq
open Collections
open Xml
open System

module ConfigFiles =
    open System.Collections.Generic

    type ProjectConfig() =
        member val Extensions = List<Dictionary<string, string>>() with get, set
        member val Steps = Dictionary<string, List<Dictionary<string, string>>>() with get, set
        member val Dependencies = List<string>() with get, set
        member val Outputs = List<string>() with get, set
        member val Ignores = List<string>() with get, set
        member val Targets = Dictionary<string, List<string>>() with get, set
        member val Tags = List<string>() with get, set

    type BuildConfig() =
        member val Dependencies = List<string>() with get, set
        member val Targets = Dictionary<string, List<string>>() with get, set
        member val Variables = Dictionary<string, string>() with get, set


type Dependencies = string list
type Outputs = string list
type TargetRules = list<string>
type Targets = Map<string, TargetRules>
type Step = {
    Command: string
    Arguments: string
}
type StepCommands = Step list
type Steps = Map<string, StepCommands>
type Tags = string list
type Variables = Map<string, string>

type ProjectConfig = {
    Dependencies: Dependencies
    Ignores: Dependencies
    Outputs: Outputs
    Targets: Targets
    Steps: Steps
    Tags: Tags
}

type BuildConfig = {
    Dependencies: Dependencies
    Targets: Targets
    Variables: Variables
}

type WorkspaceConfig = {
    Directory: string
    Build: BuildConfig
    Projects: Map<string, ProjectConfig>
}

[<Flags>]
type ExtensionCapabilities =
    | None = 0
    | Dependencies = 1
    | Steps = 2
    | Outputs = 4
    | Ignores = 8

[<AbstractClass>]
type Extension() =
    abstract Capabilities: ExtensionCapabilities with get
    abstract member GetDependencies: string list
    abstract member GetOutputs: string list
    abstract member GetIgnores: string list
    abstract member GetStep: action:string * args:Map<string, string> -> Step list


type DotnetExtension(workingDir, args) =
    inherit Extension()

    let parseDotnetDependencies workingDirectory projectFile =
        let projectFile = IO.combine workingDirectory projectFile
        let xdoc = XDocument.Load (projectFile)
        let refs = xdoc.Descendants() 
                        |> Seq.filter (fun x -> x.Name.LocalName = "ProjectReference")
                        |> Seq.map (fun x -> !> x.Attribute(NsNone + "Include") : string)
                        |> Seq.map IO.parentDirectory
                        |> Seq.distinct
                        |> List.ofSeq
        refs 

    let project = args |> Map.find "project"

    let dependencies workingDir = parseDotnetDependencies workingDir project

    override _.Capabilities = ExtensionCapabilities.Dependencies
                              ||| ExtensionCapabilities.Steps
                              ||| ExtensionCapabilities.Outputs

    override _.GetDependencies = dependencies workingDir

    override _.GetOutputs = [ "bin"; "obj" ]

    override _.GetIgnores = NotSupportedException() |> raise

    override _.GetStep(action, args) =
        let configuration = args |> Map.tryFind "configuration" |> Option.defaultValue "Debug"
        let arguments = args |> Map.tryFind "args" |> Option.defaultValue ""
        let dotnetArgs = $"{action} --no-dependencies --configuration {configuration} {arguments}"
        match action with
        | "build" | "publish" | "run" | "pack" -> [ { Command = "dotnet"; Arguments = dotnetArgs } ]
        | _ -> failwith $"Unsupported action '{action}'"

type ShellExtension() =
    inherit Extension()

    override _.Capabilities = ExtensionCapabilities.Steps

    override _.GetDependencies = NotSupportedException() |> raise

    override _.GetOutputs = NotSupportedException() |> raise

    override _.GetIgnores = NotSupportedException() |> raise

    override _.GetStep(action, args) =
        let arguments = args |> Map.tryFind "args" |> Option.defaultValue ""
        [ { Command = action; Arguments = arguments } ]

let getExtension name version workingDir args : Extension =
    match name with
    | "dotnet" -> DotnetExtension(workingDir, args)
    | "shell" -> ShellExtension()
    | _ -> failwith $"Unknown plugin {name}"

let getExtensionFromInvoke name =
    match name with
    | String.Regex "^\(([a-z]+)\)$" [name] -> Some name
    | _ -> None


let read workspaceDirectory =
    let buildFile = Path.Combine(workspaceDirectory, "BUILD")
    let buildConfig = Yaml.DeserializeFile<ConfigFiles.BuildConfig> buildFile
    let buildConfig = { Dependencies = buildConfig.Dependencies |> List.ofSeq
                        Targets = buildConfig.Targets |> Map.ofDict |> Map.map (fun _ v -> v |> List.ofSeq)
                        Variables = buildConfig.Variables |> Map.ofDict }

    let mutable projects = Map.empty

    let rec scanDependencies dependencies =
        for dependency in dependencies do
            let dependencyDirectory = IO.combine workspaceDirectory dependency

            let defaultExtensions = 
                [ "shell", getExtension "shell" "1.0.0" dependencyDirectory Map.empty ]

            // process only unknown dependency
            if projects |> Map.containsKey dependency |> not then
                let dependencyConfig =
                    match IO.combine dependencyDirectory "PROJECT" with
                    | IO.File projectFile -> Yaml.DeserializeFile<ConfigFiles.ProjectConfig> projectFile
                    | _ -> ConfigFiles.ProjectConfig()

                let getExtensionParamAndArgs (arguments: Map<string, string>) =
                    let extensionInfo, extensionArgs = arguments |> Map.partition (fun k v -> k |> getExtensionFromInvoke |> Option.isSome)
                    let extensionCommand = extensionInfo |> Seq.exactlyOne
                    let extensionName = (extensionCommand.Key |> getExtensionFromInvoke |> Option.get)
                    let extensionParam = extensionCommand.Value
                    extensionName, extensionParam, extensionArgs

                // load specified extensions
                let extensions =
                    let buildExtension (arguments: Map<string, string>) =
                        let extName, extParam, extArgs = getExtensionParamAndArgs arguments
                        let extension = getExtension extName extParam dependencyDirectory extArgs
                        extName, extension

                    dependencyConfig.Extensions
                    |> Seq.map Map.ofDict
                    |> Seq.map buildExtension
                    |> Seq.append defaultExtensions
                    |> Map.ofSeq

                let getExtensionCapabilities capability getCapability (extension: Extension) =
                    match extension.Capabilities &&& capability with
                    | ExtensionCapabilities.None -> []
                    | _ -> getCapability extension

                let getExtensionsCapabilities capability getCapability =
                    extensions
                    |> Seq.collect (fun kvp -> getExtensionCapabilities capability getCapability kvp.Value)
                    |> List.ofSeq

                let convertStepList steps =
                    steps
                    |> Seq.collect (fun args ->
                            let extName, extParam, extArgs = args |> Map.ofDict |> getExtensionParamAndArgs
                            let extension = extensions |> Map.find extName
                            getExtensionCapabilities ExtensionCapabilities.Steps (fun e -> e.GetStep(extParam, extArgs)) extension)
                    |> List.ofSeq

                // collect extension capabilities
                let extensionDependencies = getExtensionsCapabilities ExtensionCapabilities.Dependencies (fun e -> e.GetDependencies)
                let extensionOutputs = getExtensionsCapabilities ExtensionCapabilities.Outputs (fun e -> e.GetOutputs)
                let extensionIgnores = getExtensionsCapabilities ExtensionCapabilities.Ignores (fun e -> e.GetIgnores)

                let steps = dependencyConfig.Steps |> Map.ofDict |> Map.map (fun _ value -> convertStepList value)
                let dependencies = dependencyConfig.Dependencies |> List.ofSeq |> List.append extensionDependencies
                let outputs = dependencyConfig.Outputs |> List.ofSeq |> List.append extensionOutputs
                let ignores = dependencyConfig.Ignores |> List.ofSeq |> List.append outputs |> List.append extensionIgnores
                let targets = dependencyConfig.Targets |> Map.ofDict |> Map.map (fun _ v -> v |> List.ofSeq)
                let tags = dependencyConfig.Tags |> List.ofSeq

                let dependencyConfig =
                    { Dependencies = dependencies
                      Outputs = outputs
                      Ignores = ignores
                      Targets = targets
                      Steps = steps
                      Tags = tags }

                // convert relative dependencies to absolute dependencies respective to workspaceDirectory
                let dependencies =
                    dependencyConfig.Dependencies
                    |> List.map (fun dep -> IO.combine dependencyDirectory dep |> IO.relativePath workspaceDirectory)

                let dependencyConfig = { dependencyConfig
                                         with Dependencies = dependencies }
                projects <- projects |> Map.add dependency dependencyConfig

                scanDependencies dependencies

    // initial dependency list is absolute respective to workspaceDirectory
    scanDependencies buildConfig.Dependencies
    { Directory = workspaceDirectory; Build = buildConfig; Projects = projects }
