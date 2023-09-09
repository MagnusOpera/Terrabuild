module Configuration
open Legivel.Attributes
open Helpers.Collections
open Helpers
open System.IO

type Dependencies = list<string>

type Outputs = list<string>

type ProjectTargets = map<string, Dependencies>

type DependsOns = list<string>

type ProjectConfiguration = {
    [<YamlField("dependencies")>] Dependencies: Dependencies option
    [<YamlField("outputs")>] Outputs: Outputs option
    [<YamlField("targets")>] Targets: ProjectTargets option
}

type StoreType =
    | [<YamlValue("local")>] Local

type Store = {
    [<YamlField("type")>] Type: StoreType
    [<YamlField("url")>] Url: string
}

type BuildTarget = {
    [<YamlField("depends-on")>] DependsOn: DependsOns option
}

type BuildTargets = map<string, BuildTarget>

type BuildVariables = map<string, string>

type BuildConfiguration = {
    [<YamlField("store")>] Store: Store option
    [<YamlField("dependencies")>] Dependencies: Dependencies
    [<YamlField("targets")>] Targets: BuildTargets option
    [<YamlField("variables")>] Variables: BuildVariables option
}




type GlobalConfiguration = {
    Build: BuildConfiguration
    Projects: map<string, ProjectConfiguration>
}

let read workspaceDirectory =
    let buildFile = Path.Combine(workspaceDirectory, "BUILD")
    let buildConfig = Json.DeserializeFile<BuildConfiguration> buildFile

    let mutable projects = Map.empty

    let rec scanDependencies cwd dependencies =
        for dependency in dependencies do
            let dependencyDirectory = Path.Combine(cwd, dependency)
            let dependencyId = Path.GetRelativePath(workspaceDirectory, dependencyDirectory)

            // process only unknown dependency
            if projects |> Map.containsKey dependency |> not then
                let dependencyFile = Path.Combine(dependencyDirectory, "PROJECT")
                let dependencyConfig =
                    if File.Exists(dependencyFile) then
                        Json.DeserializeFile<ProjectConfiguration> dependencyFile
                    else
                        { Dependencies = None; Outputs = None; Targets = None }
                projects <- projects |> Map.add dependencyId dependencyConfig

                let dependencyRelativePathes = dependencyConfig.Dependencies |> Option.defaultValue List.empty
                scanDependencies dependencyDirectory dependencyRelativePathes

    scanDependencies workspaceDirectory buildConfig.Dependencies
    { Build = buildConfig; Projects = projects }
