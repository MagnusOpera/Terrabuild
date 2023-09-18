module Configuration
open System.IO
open System.Xml.Linq
open YamlDotNet.Serialization
open Collections
open Xml

module ConfigFiles =
    open System.Collections.Generic

    [<CLIMutable>]
    type ProjectConfig = {
        [<YamlMember(Alias = "dependencies")>] Dependencies: List<Dictionary<string, string>>
        [<YamlMember(Alias = "outputs")>] Outputs: List<string>
        [<YamlMember(Alias = "targets")>] Targets: Dictionary<string, List<string>>
        [<YamlMember(Alias = "steps")>] Steps: Dictionary<string, List<Dictionary<string, string>>>
        [<YamlMember(Alias = "tags")>] Tags: List<string>
    }

    [<CLIMutable>]
    type BuildConfig = {
        [<YamlMember(Alias = "dependencies")>] Dependencies: List<string>
        [<YamlMember(Alias = "targets")>] Targets: Dictionary<string, List<string>>
        [<YamlMember(Alias = "variables")>] Variables: Dictionary<string, string>
    }


type Dependencies = string list
type Outputs = string list
type TargetRules = list<string>
type Targets = Map<string, TargetRules>
type StepCommands = Map<string, string> list
type Steps = Map<string, StepCommands>
type Tags = string list
type Variables = Map<string, string>

type ProjectConfig = {
    Dependencies: Dependencies
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

let parseDotnetDependencies workingDirectory arguments =
    let projectFile = IO.combine workingDirectory arguments
    let xdoc = XDocument.Load (projectFile)
    let refs = xdoc.Descendants() 
                    |> Seq.filter (fun x -> x.Name.LocalName = "ProjectReference")
                    |> Seq.map (fun x -> !> x.Attribute(NsNone + "Include") : string)
                    |> Seq.map IO.parentDirectory
                    |> Seq.distinct
                    |> List.ofSeq
    refs 


let addPluginDependencies workingDirectory (dependency: Map<string, string>) =
    if dependency |> Map.containsKey "^path" then
        [ dependency["^path"] ]
    elif dependency |> Map.containsKey "^dotnet" then
        parseDotnetDependencies workingDirectory dependency["^dotnet"]
    else
        failwith "Unknown dependency type"

let read workspaceDirectory =
    let buildFile = Path.Combine(workspaceDirectory, "BUILD")
    let buildConfig = Yaml.DeserializeFile<ConfigFiles.BuildConfig> buildFile
    let buildConfig = { Dependencies = buildConfig.Dependencies |> emptyIfNull
                                       |> List.ofSeq
                        Targets = buildConfig.Targets |> emptyIfNull 
                                  |> Map.ofDict |> Map.map (fun _ v -> v |> emptyIfNull |> List.ofSeq)
                        Variables = buildConfig.Variables |> emptyIfNull
                                    |> Map.ofDict }

    let mutable projects = Map.empty

    let rec scanDependencies dependencies =
        for dependency in dependencies do
            let dependencyDirectory = IO.combine workspaceDirectory dependency

            // process only unknown dependency
            if projects |> Map.containsKey dependency |> not then
                let dependencyConfig =
                    match IO.combine dependencyDirectory "PROJECT" with
                    | IO.File projectFile ->
                        Yaml.DeserializeFile<ConfigFiles.ProjectConfig> projectFile
                    | _ ->
                        { ConfigFiles.Dependencies = null
                          ConfigFiles.Outputs = null
                          ConfigFiles.Targets = null
                          ConfigFiles.Steps = null
                          ConfigFiles.Tags = null }

                let dependencies =
                    dependencyConfig.Dependencies |> emptyIfNull
                    |> Seq.collect (fun dependency -> dependency |> Map.ofDict
                                                      |> addPluginDependencies dependencyDirectory)
                    |> List.ofSeq

                let dependencyConfig =
                    { Dependencies = dependencies
                      Outputs = dependencyConfig.Outputs |> emptyIfNull
                                |> List.ofSeq
                      Targets = dependencyConfig.Targets |> emptyIfNull
                                    |> Map.ofDict |> Map.map (fun _ v -> v |> emptyIfNull
                                                                         |> List.ofSeq)
                      Steps = dependencyConfig.Steps |> emptyIfNull
                                    |> Map.ofDict |> Map.map (fun _ v -> v |> emptyIfNull
                                                                           |> List.ofSeq
                                                                           |> List.map (fun v -> v |> Map.ofDict))
                      Tags = dependencyConfig.Tags |> emptyIfNull
                             |> List.ofSeq }

                // convert relative dependencies to absolute dependencies respective to workspaceDirectory
                let dependencies =
                    dependencyConfig.Dependencies
                    |> List.map (fun dep -> IO.combine dependencyDirectory dep
                                            |> IO.relativePath workspaceDirectory)

                let dependencyConfig = { dependencyConfig
                                         with Dependencies = dependencies }
                projects <- projects |> Map.add dependency dependencyConfig

                scanDependencies dependencies

    // initial dependency list is absolute respective to workspaceDirectory
    scanDependencies buildConfig.Dependencies
    { Directory = workspaceDirectory; Build = buildConfig; Projects = projects }
