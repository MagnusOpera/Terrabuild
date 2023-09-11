module Configuration
open Legivel.Attributes
open Helpers
open System.IO

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
    Build: BuildConfig
    Projects: Map<string, ProjectConfig>
}

let read workspaceDirectory =
    let buildFile = Path.Combine(workspaceDirectory, "BUILD")
    let buildConfig = Json.DeserializeFile<BuildConfig> buildFile

    let mutable projects = Map.empty

    let rec scanDependencies dependencies =
        for dependency in dependencies do
            let dependencyDirectory = Path.Combine(workspaceDirectory, dependency)

            // process only unknown dependency
            if projects |> Map.containsKey dependency |> not then
                let dependencyFile = Path.Combine(dependencyDirectory, "PROJECT")
                let dependencyConfig =
                    if File.Exists(dependencyFile) then
                        Json.DeserializeFile<ProjectConfig> dependencyFile
                    else
                        { Dependencies = List.empty; Outputs = List.empty; Targets = Map.empty; Tags = List.empty }

                // convert relative dependencies to absolute dependencies respective to workspaceDirectory
                let dependencies =
                    dependencyConfig.Dependencies
                    |> List.map (fun dep -> let depDir = Path.Combine(dependencyDirectory, dep)
                                            Path.GetRelativePath(workspaceDirectory, depDir))

                let dependencyConfig = { dependencyConfig
                                         with Dependencies = dependencies }
                projects <- projects |> Map.add dependency dependencyConfig

                scanDependencies dependencies

    // initial dependency list is absolute respective to workspaceDirectory
    scanDependencies buildConfig.Dependencies
    { Build = buildConfig; Projects = projects }
