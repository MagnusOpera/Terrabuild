module Configuration
open Legivel.Attributes
open Helpers
open System.IO

type Dependencies = List<string>

type Outputs = List<string>

type ProjectTargets = Map<string, Dependencies>

type DependsOns = List<string>

type ProjectConfiguration = {
    [<YamlField("dependencies")>] Dependencies: Dependencies
    [<YamlField("outputs")>] Outputs: Outputs
    [<YamlField("targets")>] Targets: ProjectTargets
}

type BuildTarget = {
    [<YamlField("depends-on")>] DependsOn: DependsOns
}

type BuildTargets = Map<string, BuildTarget>

type BuildVariables = Map<string, string>

type BuildConfiguration = {
    [<YamlField("dependencies")>] Dependencies: Dependencies
    [<YamlField("targets")>] Targets: BuildTargets
    [<YamlField("variables")>] Variables: BuildVariables
}

type GlobalConfiguration = {
    Build: BuildConfiguration
    Projects: Map<string, ProjectConfiguration>
}

let read workspaceDirectory =
    let buildFile = Path.Combine(workspaceDirectory, "BUILD")
    let buildConfig = Json.DeserializeFile<BuildConfiguration> buildFile

    let mutable projects = Map.empty

    let rec scanDependencies dependencies =
        for dependency in dependencies do
            let dependencyDirectory = Path.Combine(workspaceDirectory, dependency)

            // process only unknown dependency
            if projects |> Map.containsKey dependency |> not then
                let dependencyFile = Path.Combine(dependencyDirectory, "PROJECT")
                let dependencyConfig =
                    if File.Exists(dependencyFile) then
                        Json.DeserializeFile<ProjectConfiguration> dependencyFile
                    else
                        { Dependencies = List.empty; Outputs = List.empty; Targets = Map.empty }

                // convert relative dependencies to absolute dependencies respective to workspaceDirectory
                let dependencies =
                    dependencyConfig.Dependencies
                    |> List.map (fun dep -> let depDir = Path.Combine(dependencyDirectory, dep)
                                            Path.GetRelativePath(workspaceDirectory, depDir))

                let dependencyConfig = { dependencyConfig
                                         with Dependencies = dependencies }
                projects <- projects |> Map.add dependency dependencyConfig

                scanDependencies dependencies

    scanDependencies buildConfig.Dependencies
    { Build = buildConfig; Projects = projects }
