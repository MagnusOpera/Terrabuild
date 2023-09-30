module Configuration
open System.IO
open Collections
open System
open System.Collections.Concurrent
open Extensions

module YamlConfigFiles =
    open System.Collections.Generic

    type ProjectConfig() =
        member val Extensions = List<Dictionary<string, string>>() with get, set
        member val Steps = Dictionary<string, List<Dictionary<string, string>>>() with get, set
        member val Dependencies = List<string>() with get, set
        member val Outputs = List<string>() with get, set
        member val Ignores = List<string>() with get, set
        member val Targets = Dictionary<string, List<string>>() with get, set

    type BuildConfig() =
        member val Dependencies = List<string>() with get, set
        member val Targets = Dictionary<string, List<string>>() with get, set
        member val Variables = Dictionary<string, string>() with get, set


type Dependencies = Set<string>
type Outputs = Set<string>
type TargetRules = Set<string>
type Targets = Map<string, TargetRules>
type StepCommands = Step list
type Steps = Map<string, StepCommands>
type Tags = Set<string>
type Variables = Map<string, string>

type ProjectConfig = {
    Dependencies: Dependencies
    Ignores: Dependencies
    Outputs: Outputs
    Targets: Targets
    Steps: Steps
    Files: Set<string>
    Hash: string
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

let loadExtension name workspaceDir projectDir projectFile parameters : Extension =
    let context = { new IExtensionContext
                    with member _.WorkspaceDirectory = workspaceDir
                         member _.ProjectDirectory = projectDir
                         member _.ProjectFile = projectFile
                         member _.Parameters = parameters }

    match name with
    | "dotnet" -> Extensions.Dotnet.DotnetExtension(context)
    | "shell" -> Extensions.Shell.ShellExtension(context)
    | "docker" -> Extensions.Docker.DockerExtension(context)
    | "make" -> Extensions.Make.MakeExtension(context)
    | "echo" -> Extensions.Echo.EchoExtension(context)
    | _ -> failwith $"Unknown plugin {name}"

let read workspaceDirectory =
    let buildFile = Path.Combine(workspaceDirectory, "BUILD")
    let buildConfig = Yaml.DeserializeFile<YamlConfigFiles.BuildConfig> buildFile
    let buildConfig = { Dependencies = buildConfig.Dependencies |> Set.ofSeq
                        Targets = buildConfig.Targets |> Map.ofDict |> Map.map (fun _ v -> v |> Set.ofSeq)
                        Variables = buildConfig.Variables |> Map.ofDict }

    let processedNodes = ConcurrentDictionary<string, bool>()
    let mutable projects = Map.empty

    let getExtensionFromInvocation name =
        match name with
        | String.Regex "^\(([a-z]+)\)$" [name] -> Some name
        | _ -> None

    let rec scanDependencies dependencies =
        for dependency in dependencies do
            let projectId = IO.combine workspaceDirectory dependency
            let projectDir, projectFile = 
                match projectId with
                | IO.File projectFile -> IO.parentDirectory projectFile, IO.getFilename projectFile
                | IO.Directory projectDir -> projectDir, "PROJECT"
                | _ -> failwith $"Failed to find project '{projectId}'"

            let defaultExtensions = 
                [ "shell", loadExtension "shell" workspaceDirectory projectDir projectDir Map.empty
                  "echo", loadExtension "echo" workspaceDirectory projectDir projectDir Map.empty ]

            // process only unknown dependency
            if processedNodes.TryAdd(dependency, true) then

                // we might have landed in a directory without a configuration
                // in that case we just use the default configuration (which does nothing)
                let dependencyConfig =
                    match IO.combine projectDir projectFile with
                    | IO.File projectFile -> Yaml.DeserializeFile<YamlConfigFiles.ProjectConfig> projectFile
                    | _ -> YamlConfigFiles.ProjectConfig()


                let getExtensionParamAndArgs (arguments: Map<string, string>) =
                    let extensionInfo, extensionArgs = arguments |> Map.partition (fun k v -> k |> getExtensionFromInvocation |> Option.isSome)
                    let extensionCommand = extensionInfo |> Seq.exactlyOne
                    let extensionName = (extensionCommand.Key |> getExtensionFromInvocation |> Option.get)
                    let extensionParam = extensionCommand.Value
                    extensionName, extensionParam, extensionArgs

                // load specified extensions
                let extensions =
                    let buildExtension (arguments: Map<string, string>) =
                        let extName, extParam, extArgs = getExtensionParamAndArgs arguments
                        let extension = loadExtension extName workspaceDirectory projectDir extParam extArgs
                        extName, extension

                    dependencyConfig.Extensions
                    |> Seq.map Map.ofDict
                    |> Seq.map buildExtension
                    |> Seq.append defaultExtensions
                    |> Map.ofSeq

                let getExtensionCapabilities capability getCapability (extension: Extension) =
                    match extension.Capabilities &&& capability with
                    | Capabilities.None -> []
                    | _ -> getCapability extension

                let getExtensionsCapabilities capability getCapability =
                    extensions
                    |> Seq.collect (fun kvp -> getExtensionCapabilities capability getCapability kvp.Value)

                let extensionDependencies = getExtensionsCapabilities Capabilities.Dependencies (fun e -> e.Dependencies)

                let projectDependencies = dependencyConfig.Dependencies |> Seq.append extensionDependencies |> Set.ofSeq

                // convert relative dependencies to absolute dependencies respective to workspaceDirectory
                let projectDependencies =
                    projectDependencies
                    |> Set.map (fun dep -> IO.combine projectDir dep |> IO.relativePath workspaceDirectory)

                // we go depth-first in order to compute node hash right after
                // NOTE: this could lead to a memory usage problem
                scanDependencies projectDependencies


                let convertStepList steps =
                    steps
                    |> Seq.collect (fun args ->
                            let extName, extParam, extArgs = args |> Map.ofDict |> getExtensionParamAndArgs
                            let extension = extensions |> Map.find extName
                            getExtensionCapabilities Capabilities.Steps (fun e -> e.GetStep(extParam, extArgs)) extension)
                    |> List.ofSeq

                // collect extension capabilities
                let extensionOutputs = getExtensionsCapabilities Capabilities.Outputs (fun e -> e.Outputs)
                let extensionIgnores = getExtensionsCapabilities Capabilities.Ignores (fun e -> e.Ignores)

                let projectSteps = dependencyConfig.Steps |> Map.ofDict |> Map.map (fun _ value -> convertStepList value)
                let projectOutputs = dependencyConfig.Outputs |> Seq.append extensionOutputs |> Set.ofSeq
                let projectIgnores = dependencyConfig.Ignores |> Seq.append projectOutputs |> Seq.append extensionIgnores |> Set.ofSeq
                let projectTargets = dependencyConfig.Targets |> Map.ofDict |> Map.map (fun _ v -> v |> Set.ofSeq)

                // get dependencies on variables
                let variables =
                    let extractVariables s =
                        match s with
                        | String.Regex "\$\(([a-z]+)\)" variables -> variables
                        | _ -> []

                    projectSteps
                    |> Seq.collect (fun kvp -> kvp.Value
                                               |> List.collect (fun step -> step.Arguments |> extractVariables))
                    |> Seq.except ["terrabuild_node_hash"]
                    |> Seq.map (fun varName -> varName, buildConfig.Variables |> Map.find varName)
                    |> Map.ofSeq

                let variableHashes =
                    variables
                    |> Seq.map (fun kvp -> $"{kvp.Key} = {kvp.Value}")
                    |> String.join "\n"

                // get dependencies on files
                let files = projectDir |> IO.enumerateFilesBut projectIgnores |> Set.ofSeq
                let filesHash = files |> Seq.sort |> Hash.computeFilesSha
                let projectFiles = files |> Set.map (IO.relativePath projectDir)

                let ignoresHash = projectIgnores |> Seq.sort |> String.join "\n" |> String.sha256

                let dependenciesHash =
                    projectDependencies
                    |> Seq.map (fun dependency -> (projects |> Map.find dependency).Hash)
                    |> Seq.sort
                    |> String.join "\n" |> String.sha256

                // NOTE: this is the hash (modulo target name) used for reconcialiation across executions
                let nodeHash =
                    [ filesHash; ignoresHash; dependenciesHash; variableHashes ]
                    |> String.join "\n" |> String.sha256

                let dependencyConfig =
                    { Dependencies = projectDependencies
                      Outputs = projectOutputs
                      Ignores = projectIgnores
                      Targets = projectTargets
                      Steps = projectSteps
                      Files = projectFiles
                      Hash = nodeHash }

                projects <- projects |> Map.add dependency dependencyConfig


    // initial dependency list is absolute respective to workspaceDirectory
    scanDependencies buildConfig.Dependencies
    { Directory = workspaceDirectory; Build = buildConfig; Projects = projects }
