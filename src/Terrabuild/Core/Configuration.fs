module Configuration
open System.IO
open Collections
open System
open System.Collections.Concurrent
open MagnusOpera.PresqueYaml

type ExtensionConfig = {
    Container: YamlNodeValue<string>
    Parameters: Map<string, YamlNode>
}

type Variables = Map<string, string>

type BuildConfig = {
    Storage: string option
    SourceControl: string option
    Environments: Map<string, Variables>
    Targets: Map<string, string set>
    Extensions: Map<string, ExtensionConfig>
}

type BuilderConfig = {
    Use: string option
    With: string option
    Container: YamlNodeValue<string>
    Parameters: Map<string, YamlNode>
}

type Items = string set
type CommandConfig = Map<string, YamlNode>
type TargetRules = string set

type ProjectConfig = {
    Builders: Map<string, BuilderConfig option>
    Dependencies: Items
    Outputs: Items
    Ignores: Items
    Targets: Map<string, Items>
    Steps: Map<string, CommandConfig list>
    Labels: Items
}

[<RequireQualifiedAccess>]
type Options = {
    MaxConcurrency: int
    NoCache: bool
    Retry: bool
    CI: bool
}

type ConfigException(msg, ?innerException: Exception) =
    inherit Exception(msg, innerException |> Option.defaultValue null)

    static member Raise(msg, ?innerException) =
        ConfigException(msg, ?innerException=innerException) |> raise

[<RequireQualifiedAccess>]
type ContaineredCommand = {
    Container: string option
    Command: string
    Arguments: string
    Cache: Extensions.Cacheability
}

[<RequireQualifiedAccess>]
type Step = {
    Hash: string
    Variables: Map<string, string>
    CommandLines: ContaineredCommand list
}

type Steps = Map<string, Step>

[<RequireQualifiedAccess>]
type Project = {
    Dependencies: Items
    Files: string set
    Ignores: Items
    Outputs: Items
    Targets: Map<string, Items>
    Steps: Steps
    Hash: string
    Variables: Variables
    Labels: string set
}

[<RequireQualifiedAccess>]
type WorkspaceConfig = {
    Storage: Storages.Storage
    SourceControl: SourceControls.SourceControl
    Directory: string
    Dependencies: Items
    Build: BuildConfig
    Projects: Map<string, Project>
    Environment: string
}


module ExtensionLoaders =
    open Extensions

    let loadExtension name context : Extensions.IExtension =
        let factory: IExtensionFactory =
            match name with
            | "dotnet" -> DotnetFactory()
            | "npm" -> NpmFactory()
            | "terraform" -> TerraformFactory()
            | "shell" -> ShellFactory()
            | "docker" -> DockerFactory()
            | "make" -> MakeFactory()
            | "echo" -> EchoFactory()
            | _ -> failwith $"Unknown plugin '{name}'"
        factory.Create(context)

    let loadStorage name : Storages.Storage =
        match name with
        | None -> Storages.Local()
        | Some "azureblob" -> Storages.MicrosoftBlobStorage()
        | _ -> failwith $"Unknown storage '{name}'"

    let loadSourceControl name: SourceControls.SourceControl =
        match name with
        | None -> SourceControls.Local()
        | Some "github" -> SourceControls.GitHub()
        | _ -> failwith $"Unknown source control '{name}'"

module ProjectConfigParser =

    [<RequireQualifiedAccess>]
    type StepDefinition = {
        Extension: Extensions.IExtension
        Command: string
        Parameters: CommandConfig
        Container: string option
    }
    
    [<RequireQualifiedAccess>]
    type ProjectDefinition = {
        Dependencies: Items
        Ignores: Items
        Outputs: Items
        Targets: Map<string, Items>
        StepDefinitions: Map<string, StepDefinition list>
        Labels: string set
    }

    let getExtensionFromInvocation name =
        match name with
        | String.Regex "^\(([a-zA-Z][_a-zA-Z0-9]+)\)$" [name] -> Some name
        | _ -> None

    let parse workspaceDir (buildConfig: BuildConfig) projectDir projectFile defaultExtensions shared commit branchOrTag =
        let projectFilename = IO.combinePath projectDir projectFile
        // we might have landed in a directory without a configuration
        // in that case we just use the default configuration (which does nothing)
        let projectDocument =
            match projectFilename with
            | IO.File projectFile ->
                match Yaml.loadDocument projectFile with
                | Ok doc -> doc
                | Error err -> ConfigException.Raise($"PROJECT '{projectFilename}' is invalid", err)
            | _ -> YamlNode.None
        let projectConfig = Yaml.deserialize<ProjectConfig> projectDocument
        
        let projectOutputs = projectConfig.Outputs
        let projectIgnores = projectConfig.Ignores
        let projectTargets = projectConfig.Targets
        let projectDependencies = projectConfig.Dependencies
        let labels = projectConfig.Labels

        let defaultBuilder = {
            BuilderConfig.Container = YamlNodeValue.Undefined
            BuilderConfig.Use = None
            BuilderConfig.With = None
            BuilderConfig.Parameters = Map.empty 
        }
        
        let projectBuilders =
            projectConfig.Builders
            |> Map.map (fun alias builderConfig ->
                let builderConfig =
                    builderConfig
                    |> Option.defaultValue defaultBuilder
                
                // load extension first
                let builderUse = builderConfig.Use |> Option.defaultValue alias
                let builderWith = builderConfig.With
                let context = { Extensions.Context.Directory = projectDir
                                Extensions.Context.With = builderWith
                                Extensions.Context.CI = shared }

                let builder = ExtensionLoaders.loadExtension builderUse context

                // builder override ?
                let paramsOverride =
                    buildConfig.Extensions
                    |> Map.tryFind builderUse
                    |> Option.map (fun extension -> extension.Parameters)
                    |> Option.defaultValue Map.empty
                let builderParams =
                    builderConfig.Parameters
                    |> Map.replace paramsOverride

                // container override ?
                let containerOverride =
                    buildConfig.Extensions
                    |> Map.tryFind builderUse
                    |> Option.map (fun extension -> extension.Container)
                    |> Option.defaultValue builderConfig.Container
                let container =
                    match containerOverride with
                    | YamlNodeValue.Value container -> Some container
                    | YamlNodeValue.None -> None
                    | YamlNodeValue.Undefined -> builder.Container

                {| Extension = builder; Parameters = builderParams; Container = container |})

        // collect extension capabilities
        let builderOutputs =
            projectBuilders
            |> Seq.collect (fun (KeyValue(_, builderInfo)) -> builderInfo.Extension.Outputs)
            |> Set

        let builderIgnores =
            projectBuilders
            |> Seq.collect (fun (KeyValue(_, builderInfo)) -> builderInfo.Extension.Ignores)
            |> Set

        let builderDependencies =
            projectBuilders
            |> Seq.collect (fun (KeyValue(_, builderInfo)) -> builderInfo.Extension.Dependencies)
            |> Set

        let projectOutputs = projectOutputs + builderOutputs
        let projectIgnores = projectIgnores + builderIgnores

        // convert relative dependencies to absolute dependencies respective to workspaceDirectory
        let projectDependencies =
            (projectDependencies + builderDependencies)
            |> Set.map (fun dep -> IO.combinePath projectDir dep
                                    |> IO.relativePath workspaceDir)

        let projectBuilders = defaultExtensions |> Map.replace projectBuilders


        let projectStepDefinitions =
            projectConfig.Steps
            |> Map.map (fun _ commands ->
                commands |> List.map (fun command ->
                    let builderInfo, stepParams = command |> Map.partition (fun k _ -> k |> getExtensionFromInvocation |> Option.isSome)
                    let builderInfo = builderInfo |> Seq.exactlyOne
                    let builderName, builderCommand = builderInfo.Key |> getExtensionFromInvocation |> Option.get, builderInfo.Value |> Yaml.toString
                    let builderInfo = projectBuilders |> Map.find builderName

                    let stepParams = stepParams |> Map.replace builderInfo.Parameters

                    { StepDefinition.Extension = builderInfo.Extension
                      StepDefinition.Container = builderInfo.Container
                      StepDefinition.Command = builderCommand
                      StepDefinition.Parameters = stepParams }))

        { ProjectDefinition.Dependencies = projectDependencies
          ProjectDefinition.Ignores = projectIgnores
          ProjectDefinition.Outputs = projectOutputs
          ProjectDefinition.Targets = projectTargets
          ProjectDefinition.StepDefinitions = projectStepDefinitions
          ProjectDefinition.Labels = labels }




let read workspaceDir (options: Options) environment labels variables =
    let buildFile = Path.Combine(workspaceDir, "WORKSPACE")
    let buildDocument =
        match Yaml.loadDocument buildFile with
        | Ok doc -> doc
        | Error err -> ConfigException.Raise($"Configuration '{buildFile}' is invalid", err)
    let buildConfig = Yaml.deserialize<BuildConfig> buildDocument

    // variables
    let environments = buildConfig.Environments
    let envVariables =
        match environments |> Map.tryFind environment with
        | Some variables -> variables
        | _ ->
            match environment with
            | "default" -> Map.empty
            | _ -> ConfigException.Raise($"Environment '{environment}' not found")
    let buildVariables =
        envVariables
        |> Map.replace variables

    // storage
    let storage =
        buildConfig.Storage
        |> Option.bind (fun x -> if options.NoCache then None else Some x)
        |> ExtensionLoaders.loadStorage

    // source control
    let sourceControl =
        if options.CI then
            buildConfig.SourceControl
            |> ExtensionLoaders.loadSourceControl
        else
            ExtensionLoaders.loadSourceControl None
    let commit = sourceControl.HeadCommit
    let branchOrTag = sourceControl.BranchOrTag

    // extensions
    let defaultExtensions =
        let context = { Extensions.Context.Directory = workspaceDir
                        Extensions.Context.With = None
                        Extensions.Context.CI = options.CI }

        Map [ "shell", {| Extension = ExtensionLoaders.loadExtension "shell" context
                          Parameters = Map.empty
                          Container = None |}
              "echo", {| Extension = ExtensionLoaders.loadExtension "echo" context
                         Parameters = Map.empty
                         Container = None |} ]

    let processedNodes = ConcurrentDictionary<string, bool>()

    let rec scanDependency projects project =
        let projectId = IO.combinePath workspaceDir project
        let projectDir, projectFile = 
            match projectId with
            | IO.Directory projectDir -> projectDir, "PROJECT"
            | IO.File _ -> ConfigException.Raise($"Dependency '{project}' is not a directory")
            | _ -> failwith $"Failed to find project {projectId}"

        // process only unknown dependency
        if processedNodes.TryAdd(project, true) then
            let projectDef = ProjectConfigParser.parse workspaceDir buildConfig projectDir projectFile defaultExtensions options.CI commit branchOrTag

            // we go depth-first in order to compute node hash right after
            // NOTE: this could lead to a memory usage problem
            let projects: Map<string, Project> =
                try
                    scanDependencies projects projectDef.Dependencies
                with
                    ex ->
                        ConfigException.Raise($"while processing '{project}'", ex)


            // check for circular or missing dependencies
            for childDependency in projectDef.Dependencies do
                if projects |> Map.tryFind childDependency |> Option.isNone then
                    ConfigException.Raise($"Circular dependencies between {project} and {childDependency}")



            // get dependencies on files
            let files = projectDir |> IO.enumerateFilesBut (projectDef.Outputs + projectDef.Ignores) |> Set
            let filesHash =
                files
                |> Seq.sort
                |> Hash.computeFilesSha

            let projectSteps =
                projectDef.StepDefinitions
                |> Map.map (fun targetId steps ->
                    steps
                    |> List.collect (fun stepDef ->
                        let stepParams =
                            stepDef.Parameters
                            |> Map.add "nodeHash" (YamlNode.Scalar "$(terrabuild_node_hash)")
                        let stepArgsType = stepDef.Extension.GetStepParameters stepDef.Command
                        let stepParameters =
                            stepArgsType
                            |> Option.map (fun stepArgsType -> Yaml.deserializeType(stepArgsType, YamlNode.Mapping stepParams))
                            |> Option.defaultValue null

                        let cmds = stepDef.Extension.BuildStepCommands(stepDef.Command, stepParameters)

                        cmds
                        |> List.map (fun cmd ->
                            { ContaineredCommand.Container = stepDef.Container
                              ContaineredCommand.Command = cmd.Command
                              ContaineredCommand.Arguments = cmd.Arguments
                              ContaineredCommand.Cache = cmd.Cache })
                    )
                )
 
            let variables =
                projectDef.StepDefinitions
                |> Seq.collect (fun l -> l.Value)
                |> Seq.collect (fun stepDef ->
                    let prms = Yaml.dumpAsString (YamlNode.Mapping stepDef.Parameters)
                    String.AllMatches "\$\(([a-zA-Z][_a-zA-Z0-9]+)\)" prms)
                |> Set
                |> Set.remove "terrabuild_node_hash"
                |> Seq.map (fun varName ->
                    match buildVariables |> Map.tryFind varName with
                    | Some value -> varName, value
                    | _ -> ConfigException.Raise($"Variable '{varName}' is not defined in environment '{environment}'"))
                |> Map
                |> Map.add "terrabuild_branch_or_tag" (branchOrTag.Replace("/", "-"))

            let dependenciesHash =
                projectDef.Dependencies
                |> Seq.map (fun dependency -> 
                    match projects |> Map.tryFind dependency with
                    | Some project -> project.Hash
                    | _ -> ConfigException.Raise($"Circular dependencies between '{project}' and '{dependency}'")
                )
                |> Seq.sort
                |> String.join "\n"
                |> String.sha256

            // NOTE: this is the hash (modulo target name) used for reconcialiation across executions
            let nodeHash =
                [ project; filesHash; dependenciesHash ]
                |> String.join "\n"
                |> String.sha256

            let variables =
                variables
                |> Map.add "terrabuild_node_hash" nodeHash

            let projectSteps =
                projectSteps
                |> Map.map (fun _ stepCommandLines ->
                    // collect variables for this step
                    let variableNames =
                        stepCommandLines
                        |> Seq.collect (fun stepDef -> String.AllMatches "\$\(([a-zA-Z][_a-zA-Z0-9]+)\)" stepDef.Arguments)
                        |> Set

                    let variableValues =
                        variableNames
                        |> Seq.map (fun varName ->
                            match variables |> Map.tryFind varName with
                            | Some value -> varName, value
                            | _ -> ConfigException.Raise($"Variable {varName} is not defined in \"{environment}\""))
                        |> Map

                    let stepWithValues =
                        stepCommandLines
                        |> List.map (fun step ->
                            { step
                              with Arguments = variableValues
                                               |> Map.fold (fun acc key value -> acc |> String.replace $"$({key})" value) step.Arguments })

                    let variableHash =
                        variableValues
                        |> Seq.map (fun kvp -> $"{kvp.Key} = {kvp.Value}")
                        |> String.join "\n"
                        |> String.sha256

                    let stepHash =
                        stepWithValues
                        |> Seq.map (fun step -> $"{step.Container} {step.Command} {step.Arguments}")
                        |> String.join "\n"
                        |> String.sha256

                    let hash =
                        [ stepHash; variableHash ]
                        |> String.join "\n"
                        |> String.sha256

                    { Step.Hash = hash
                      Step.Variables = variableValues
                      Step.CommandLines = stepWithValues }
                )

            let projectConfig =
                { Project.Dependencies = projectDef.Dependencies
                  Project.Files = files
                  Project.Outputs = projectDef.Outputs
                  Project.Ignores = projectDef.Ignores
                  Project.Targets = projectDef.Targets
                  Project.Steps = projectSteps
                  Project.Hash = nodeHash
                  Project.Variables = variables
                  Project.Labels = projectDef.Labels }

            projects |> Map.add project projectConfig
        else
            projects

    and scanDependencies projects dependencies =
        let mutable projects = projects
        for project in dependencies do
            projects <- scanDependency projects project
        projects

    // scan for projects
    let rec findDependencies dir =
        seq {
            let projectFile =  IO.combinePath dir "PROJECT" 
            match projectFile with
            | IO.File file ->
                file |> IO.parentDirectory |> IO.relativePath workspaceDir
            | _ ->
                for subdir in dir |> IO.enumerateDirs do
                    yield! findDependencies subdir
        }

    let dependencies =
        workspaceDir
        |> findDependencies
        |> Set

    let projects = scanDependencies Map.empty dependencies

    // select dependencies with labels if any
    let dependencies =
        match labels with
        | Some labels ->
            projects
             |> Seq.choose (fun (KeyValue(dependency, config)) -> if Set.intersect config.Labels labels <> Set.empty then Some dependency else None)
        | _ -> projects.Keys
        |> Set

    { WorkspaceConfig.Directory = workspaceDir
      WorkspaceConfig.Dependencies = dependencies
      WorkspaceConfig.Storage = storage
      WorkspaceConfig.SourceControl = sourceControl
      WorkspaceConfig.Build = buildConfig
      WorkspaceConfig.Projects = projects
      WorkspaceConfig.Environment = environment }
