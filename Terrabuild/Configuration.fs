module Configuration
open Legivel.Attributes
open Helpers
open Helpers.String
open System.IO
open System.Xml.Linq
open Helpers.Xml

type ProjectTarget = {
    [<YamlField("depends-on")>] DependsOn: List<string>
    [<YamlField("steps")>] Steps: string list
}

type ProjectConfig = {
    [<YamlField("dependencies")>] Dependencies: List<string>
    [<YamlField("outputs")>] Outputs: List<string>
    [<YamlField("targets")>] Targets: Map<string, ProjectTarget>
    [<YamlField("tags")>] Tags: List<string>
}

type BuildTarget = {
    [<YamlField("depends-on")>] DependsOn: List<string>
}

type BuildVariables = Map<string, string>

type BuildConfig = {
    [<YamlField("dependencies")>] Dependencies: List<string>
    [<YamlField("targets")>] Targets: Map<string, BuildTarget>
    [<YamlField("variables")>] Variables: BuildVariables option
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


let addPluginDependencies workingDirectory dependency =
    let additionalDependencies =
        match dependency with
        | Regex "^([a-z]+):(.*)$" [plugin; arguments] ->
            match plugin with
            | "dotnet" -> parseDotnetDependencies workingDirectory arguments
            | _ -> failwith $"unknown dependency plugin {plugin}"
        | _ -> []
    additionalDependencies

let read workspaceDirectory =
    let buildFile = Path.Combine(workspaceDirectory, "BUILD")
    let buildConfig = Yaml.DeserializeFile<BuildConfig> buildFile

    let mutable projects = Map.empty

    let rec scanDependencies dependencies =
        for dependency in dependencies do
            let dependencyDirectory = IO.combine workspaceDirectory dependency

            // process only unknown dependency
            if projects |> Map.containsKey dependency |> not then
                let dependencyConfig =
                    match IO.combine dependencyDirectory "PROJECT" with
                    | IO.File projectFile ->
                        Yaml.DeserializeFile<ProjectConfig> projectFile
                    | _ ->
                        { Dependencies = List.empty
                          Outputs = List.empty
                          Targets = Map.empty
                          Tags = List.empty }

                let additionalDependencies = 
                    dependencyConfig.Dependencies
                    |> List.collect (addPluginDependencies dependencyDirectory)

                let dependencyConfig = { dependencyConfig
                                         with Dependencies = additionalDependencies }

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
