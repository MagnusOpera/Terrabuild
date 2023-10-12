module Configuration
open System.IO
open Collections
open System
open System.Collections.Concurrent

module YamlConfigFiles =
    open System.Collections.Generic

    type BuilderConfig() =
        member val Use: string = null with get, set
        member val With = "" with get, set
        member val Parameters = Dictionary<string, string>() with get, set

    type ProjectConfig() =
        member val Builders = Dictionary<string, BuilderConfig>() with get, set
        member val Steps = Dictionary<string, List<Dictionary<string, string>>>() with get, set
        member val Dependencies = List<string>() with get, set
        member val Outputs = List<string>() with get, set
        member val Ignores = List<string>() with get, set
        member val Targets = Dictionary<string, List<string>>() with get, set

    type BuildConfig() =
        member val Storage = "" with get, set
        member val Dependencies = List<string>() with get, set
        member val Targets = Dictionary<string, List<string>>() with get, set
        member val Environments = Dictionary<string, Dictionary<string, string>>() with get, set
        member val Extensions = Dictionary<string, Dictionary<string, string>>() with get, set

type Dependencies = Set<string>
type Outputs = Set<string>
type TargetRules = Set<string>
type Targets = Map<string, TargetRules>
type StepCommands = Extensions.Step list
type Steps = Map<string, StepCommands>
type Tags = Set<string>
type Variables = Map<string, string>
type ExtensionConfigs = Map<string, Map<string, string>>

type ProjectConfig = {
    Dependencies: Dependencies
    Ignores: Dependencies
    Outputs: Outputs
    Targets: Targets
    Steps: Steps
    Files: Set<string>
    Hash: string
    Variables: Variables
}

type BuildConfig = {
    Dependencies: Dependencies
    Targets: Targets
    Variables: Variables
}

type WorkspaceConfig = {
    Storage: Storages.Storage option
    Directory: string
    Build: BuildConfig
    Projects: Map<string, ProjectConfig>
    Environment: string
}

let private loadExtension name projectFile parameters projectDir shared : Extensions.Extension =
    let context = { new Extensions.IContext
                    with member _.ProjectDirectory = projectDir
                         member _.ProjectFile = projectFile
                         member _.Parameters = parameters
                         member _.Shared = shared }

    match name with
    | "dotnet" -> Extensions.Dotnet(context)
    | "shell" -> Extensions.Shell(context)
    | "docker" -> Extensions.Docker(context)
    | "make" -> Extensions.Make(context)
    | "echo" -> Extensions.Echo(context)
    | _ -> failwith $"Unknown plugin {name}"

let private loadStorage name : Storages.Storage option =
    match name with
    | "azureblob" -> Storages.MicrosoftBlobStorage() :> Storages.Storage |> Some
    | _ -> None

type ConfigException(msg, innerException: Exception) =
    inherit Exception(msg, innerException)

let read workspaceDirectory shared environment =
    let buildFile = Path.Combine(workspaceDirectory, "BUILD")
    let buildConfig = Yaml.DeserializeFile<YamlConfigFiles.BuildConfig> buildFile

    let storage =
        if shared then loadStorage buildConfig.Storage
        else None

    // get variables from environment
    let variables =
        match buildConfig.Environments.TryGetValue environment with
        | true, vars ->
            vars |> Map.ofDict
        | _ ->
            match environment with
            | "default" -> Map.empty
            | _ ->
                ConfigException($"Environment {environment} not found", null)
                |> raise

    // default configuration for extensions
    let extensionConfigs =
        buildConfig.Extensions
        |> Map.ofDict
        |> Map.map (fun _ config -> config |> Map.ofDict)

    let buildConfig = { Dependencies = buildConfig.Dependencies |> Set.ofSeq
                        Targets = buildConfig.Targets |> Map.ofDict |> Map.map (fun _ v -> v |> Set.ofSeq)
                        Variables = variables }

    let processedNodes = ConcurrentDictionary<string, bool>()
    let mutable projects = Map.empty

    let buildVariables = buildConfig.Variables

    let getExtensionFromInvocation name =
        match name with
        | String.Regex "^\(([a-z]+)\)$" [name] -> Some name
        | _ -> None

    let rec scanDependencies dependencies =
        for dependency in dependencies do
            let projectId = IO.combinePath workspaceDirectory dependency
            let projectDir, projectFile = 
                match projectId with
                | IO.File projectFile -> IO.parentDirectory projectFile, IO.getFilename projectFile
                | IO.Directory projectDir -> projectDir, "PROJECT"
                | _ -> failwith $"Failed to find project '{projectId}'"

            let defaultExtensions = 
                Map [ "shell", loadExtension "shell" projectDir Map.empty projectDir shared
                      "echo", loadExtension "echo" projectDir Map.empty projectDir shared ]

            // process only unknown dependency
            if processedNodes.TryAdd(dependency, true) then

                // we might have landed in a directory without a configuration
                // in that case we just use the default configuration (which does nothing)
                let dependencyConfig =
                    match IO.combinePath projectDir projectFile with
                    | IO.File projectFile -> Yaml.DeserializeFile<YamlConfigFiles.ProjectConfig> projectFile
                    | _ -> YamlConfigFiles.ProjectConfig()


                let getExtensionParamAndArgs (arguments: Map<string, string>) =
                    let extensionInfo, extensionArgs = arguments |> Map.partition (fun k v -> k |> getExtensionFromInvocation |> Option.isSome)
                    let extensionCommand = extensionInfo |> Seq.exactlyOne
                    let extensionName = (extensionCommand.Key |> getExtensionFromInvocation |> Option.get)
                    let extensionParam = extensionCommand.Value
                    extensionName, extensionParam, extensionArgs

                // load specified extensions
                let builders =
                    dependencyConfig.Builders
                    |> Map.ofDict
                    |> Map.map (fun alias config ->
                        let useConfig =
                            match config.Use with
                            | null -> alias
                            | _ -> config.Use
                        let withConfig =
                            config.With
                        let baseConfig =
                            match extensionConfigs |> Map.tryFind useConfig with
                            | Some baseConfig -> baseConfig
                            | _ -> Map.empty
                        let buildConfig =
                            baseConfig
                            |> Map.replace (config.Parameters |> Map.ofDict)
                        loadExtension useConfig withConfig buildConfig projectDir shared)
                    |> Map.replace defaultExtensions

                let getBuilderCapabilities capability getCapability (extension: Extensions.Extension) =
                    match extension.Capabilities &&& capability with
                    | Extensions.Capabilities.None -> []
                    | _ -> getCapability extension

                let getExtensionsCapabilities capability getCapability =
                    builders
                    |> Seq.collect (fun (KeyValue(_, extension)) -> getBuilderCapabilities capability getCapability extension)

                let builderDependencies = getExtensionsCapabilities Extensions.Capabilities.Dependencies (fun e -> e.Dependencies)

                let projectDependencies = dependencyConfig.Dependencies |> Seq.append builderDependencies |> Set.ofSeq

                // convert relative dependencies to absolute dependencies respective to workspaceDirectory
                let projectDependencies =
                    projectDependencies
                    |> Set.map (fun dep -> IO.combinePath projectDir dep |> IO.relativePath workspaceDirectory)

                // we go depth-first in order to compute node hash right after
                // NOTE: this could lead to a memory usage problem
                try
                    scanDependencies projectDependencies
                with
                    ex ->
                        ConfigException($"while processing {dependency}", ex)
                        |> raise

                let convertStepList steps =
                    steps
                    |> Seq.collect (fun args ->
                            let extName, extParam, extArgs = args |> Map.ofDict |> getExtensionParamAndArgs
                            let builder = builders |> Map.find extName
                            getBuilderCapabilities Extensions.Capabilities.Steps (fun e -> e.GetStep(extParam, extArgs)) builder)
                    |> List.ofSeq

                // collect extension capabilities
                let extensionOutputs = getExtensionsCapabilities Extensions.Capabilities.Outputs (fun e -> e.Outputs)
                let extensionIgnores = getExtensionsCapabilities Extensions.Capabilities.Ignores (fun e -> e.Ignores)

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
                    |> Seq.map (fun varName ->
                        match buildVariables |> Map.tryFind varName with
                        | Some value -> varName, value
                        | _ -> ConfigException($"Variable {varName} has no value", null) |> raise)
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

                // check for circular or missing dependencies
                for childDependency in projectDependencies do
                    if projects |> Map.tryFind childDependency |> Option.isNone then
                        ConfigException($"Circular dependencies between {dependency} and {childDependency}", null)
                        |> raise

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
                      Hash = nodeHash
                      Variables = variables }

                projects <- projects |> Map.add dependency dependencyConfig


    // initial dependency list is absolute respective to workspaceDirectory
    scanDependencies buildConfig.Dependencies
    { Storage = storage
      Directory = workspaceDirectory
      Build = buildConfig
      Projects = projects
      Environment = environment }
