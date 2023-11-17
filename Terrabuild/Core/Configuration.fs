module Configuration
open System.IO
open Collections
open System
open System.Collections.Concurrent
open MagnusOpera.PresqueYaml




type ExtensionConfig = {
    Container: YamlNodeValue<string>
    Parameters: YamlNode
}

type VariablesConfig = Map<string, string>

type BuildConfig = {
    Storage: string option
    SourceControl: string option
    Targets: Map<string, string list>
    Environments: Map<string, VariablesConfig>
    Extensions: Map<string, ExtensionConfig>
}



type BuilderConfig = {
    Use: string option
    With: string option
    Container: YamlNodeValue<string>
    Parameters: YamlNode
}

type CommandConfig = Map<string, string>

type ProjectConfig = {
    Builders: Map<string, BuilderConfig>
    Dependencies: string list
    Targets: Map<string, string list>
    Steps: Map<string, YamlNodeValue<CommandConfig> list>
    Labels: string list
}







[<RequireQualifiedAccess>]
type Options = {
    MaxConcurrency: int
    NoCache: bool
    Retry: bool
    CI: bool
}

type ConfigException(msg, innerException: Exception) =
    inherit Exception(msg, innerException)


type Dependencies = string set
type Paths = string set
type TargetRules = string set
type Targets = Map<string, TargetRules>

[<RequireQualifiedAccess>]
type ContaineredCommandLine = {
    Container: string option
    Command: string
    Arguments: string
    Cache: Extensions.Cacheability
}

[<RequireQualifiedAccess>]
type Step = {
    Hash: string
    Variables: Map<string, string>
    CommandLines: ContaineredCommandLine list
}

type Steps = Map<string, Step>
type Variables = Map<string, string>
type ExtensionConfigs = Map<string, Map<string, string>>


[<RequireQualifiedAccess>]
type ProjectConfig = {
    Dependencies: Dependencies
    Files: string set
    Ignores: Paths
    Outputs: Paths
    Targets: Targets
    Steps: Steps
    Hash: string
    Variables: Variables
    Labels: string set
}

[<RequireQualifiedAccess>]
type BuildConfig = {
    Targets: Targets
    Variables: Variables
}

[<RequireQualifiedAccess>]
type WorkspaceConfig = {
    Storage: Storages.Storage
    SourceControl: SourceControls.SourceControl
    Directory: string
    Dependencies: Dependencies
    Build: BuildConfig
    Projects: Map<string, ProjectConfig>
    Environment: string
}


module ExtensionLoaders =

    let loadExtension name context : Extensions.Extension =
        match name with
        | "dotnet" -> Extensions.Dotnet(context)
        | "npm" -> Extensions.Npm(context)
        | "terraform" -> Extensions.Terraform(context)
        | "shell" -> Extensions.Shell(context)
        | "docker" -> Extensions.Docker(context)
        | "make" -> Extensions.Make(context)
        | "echo" -> Extensions.Echo(context)
        | _ -> failwith $"Unknown plugin '{name}'"

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

module BuildConfigParser =

    let parse buildDocument environment =
        // targets
        let targets =
            match buildDocument |> Yaml.query "/targets" with
            | Some (YamlNode.Mapping mapping) -> mapping |> Map.map (fun _ -> Yaml.deserialize<Set<string>>)
            | _ -> Map.empty

        // variables
        let environments =
            match buildDocument |> Yaml.query "/environments" with
            | Some (YamlNode.Mapping mapping) -> mapping |> Map.map (fun _ -> Yaml.deserialize<Map<string, string>>)
            | Some _ -> failwith "Invalid configuration for environments in BUILD configuration"
            | None -> Map.empty

        let variables =
            match environments |> Map.tryFind environment with
            | Some variables -> variables
            | _ ->
                match environment with
                | "default" -> Map.empty
                | _ ->
                    ConfigException($"Environment '{environment}' not found", null)
                    |> raise

        let buildConfig = { BuildConfig.Targets = targets
                            BuildConfig.Variables = variables }
        buildConfig






module ProjectConfigParser =

    [<RequireQualifiedAccess>]
    type StepDefinition = {
        Extension: Extensions.Extension
        Command: string
        Parameters: Map<string, YamlNode>
        Container: string option
    }

    [<RequireQualifiedAccess>]
    type ProjectDefinition = {
        Dependencies: Dependencies
        Ignores: Paths
        Outputs: Paths
        Targets: Targets
        StepDefinitions: Map<string, StepDefinition list>
        Labels: string set
    }

    let getExtensionFromInvocation name =
        match name with
        | String.Regex "^\(([a-zA-Z][_a-zA-Z0-9]+)\)$" [name] -> Some name
        | _ -> None

    let parse projectId workspaceDir buildDocument projectDir projectFile defaultExtensions shared commit branchOrTag =
        let projectFilename = IO.combinePath projectDir projectFile
        // we might have landed in a directory without a configuration
        // in that case we just use the default configuration (which does nothing)
        let projectDocument =
            match projectFilename with
            | IO.File projectFile ->
                match Yaml.loadDocument projectFile with
                | Ok doc -> doc
                | Error err -> ConfigException($"PROJECT '{projectFilename}' is invalid", err) |> raise
            | _ -> YamlNode.None

        let projectOutputs =
            projectDocument
            |> Yaml.query "/outputs"
            |> Yaml.toOptionalStringList
            |> Set

        let projectIgnores =
            projectDocument
            |> Yaml.query "/ignores"
            |> Yaml.toOptionalStringList
            |> Set

        let projectTargets =
            match projectDocument |> Yaml.query "/targets" with
            | Some (YamlNode.Mapping mapping) -> mapping |> Map.map (fun _ -> Yaml.deserialize<Set<string>>)
            | Some _ -> ConfigException($"Expecting mapping  for element /targets in PROJECT '{projectFilename}'", null) |> raise
            | _ -> Map.empty

        let projectDependencies =
            projectDocument
            |> Yaml.query "/dependencies"
            |> Yaml.toOptionalStringList
            |> Set

        let labels =
            match projectDocument |> Yaml.query "/labels" with
            | Some (YamlNode.Sequence sequence as node) -> Yaml.deserialize<Set<string>> node
            | Some _ -> ConfigException($"Expecting list for element /labels", null) |> raise
            | _ -> Set.empty

        let projectBuilders =
            match projectDocument |> Yaml.query "/builders" with
            | Some (YamlNode.Mapping builderMappings) ->
                builderMappings |> Map.map (fun alias mapping ->
                    let builderUse =
                        mapping |> Yaml.query "use" |> Yaml.toOptionalString |> Option.defaultValue alias

                    let builderWith =
                        mapping |> Yaml.query "with" |> Yaml.toOptionalString

                    let builderContainer =
                        mapping |> Yaml.query "container" |> Yaml.toOptionalString
                        |> Option.orElse (buildDocument |> Yaml.query $"/extensions/{builderUse}/container" |> Yaml.toOptionalString) 

                    let builderParams =
                        let configBuilderParams =
                            match buildDocument |> Yaml.query $"/extensions/{builderUse}/parameters" with
                            | Some (YamlNode.Mapping mapping) -> mapping
                            | Some _ -> ConfigException($"Expecting mapping for element /extensions/{builderUse}/parameters in PROJECT '{projectFilename}'", null) |> raise
                            | _ -> Map.empty

                        let configProjectParams =
                            match mapping |> Yaml.query $"parameters" with
                            | Some (YamlNode.Mapping mapping) -> mapping
                            | Some _ -> ConfigException($"Expecting mapping for element /builders/{builderUse}/parameters in PROJECT '{projectFilename}'", null) |> raise
                            | _ -> Map.empty

                        let builderParams = configBuilderParams |> Map.replace configProjectParams
                        builderParams
 
                    let context = { new Extensions.IContext
                                    with member _.Directory = projectDir
                                         member _.With = builderWith
                                         member _.CI = shared }
  
                    let builder = ExtensionLoaders.loadExtension builderUse context 
                    {| Extension = builder; Parameters = builderParams; Container = builderContainer |})
            | Some _ -> ConfigException($"Project '{projectId}' has malformed builders mapping", null) |> raise
            | _ -> Map.empty

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
            match projectDocument |> Yaml.query "/steps" with
            | Some (YamlNode.Mapping stepMappings) ->
                stepMappings |> Map.map (fun _ stepMapping ->
                    match stepMapping with
                    | YamlNode.Sequence actions ->
                        actions |> List.map (fun action ->
                            match action with
                            | YamlNode.Mapping actionConfig ->
                                let builderInfo, stepParams = actionConfig |> Map.partition (fun k _ -> k |> getExtensionFromInvocation |> Option.isSome)
                                let builderInfo = builderInfo |> Seq.exactlyOne
                                let builderName, builderCommand = builderInfo.Key |> getExtensionFromInvocation |> Option.get, builderInfo.Value |> Yaml.toString
                                let builderInfo = projectBuilders |> Map.find builderName

                                let stepParams =
                                    builderInfo.Parameters
                                    |> Map.replace stepParams 

                                let container =
                                    builderInfo.Container
                                    |> Option.orElse builderInfo.Extension.Container

                                { StepDefinition.Extension = builderInfo.Extension
                                  StepDefinition.Command = builderCommand
                                  StepDefinition.Parameters = stepParams
                                  StepDefinition.Container = container }
                            | _ -> failwith "Expecting mapping")
                    | _ -> failwith "Expecting sequence")
            | _ -> Map.empty

        { ProjectDefinition.Dependencies = projectDependencies
          ProjectDefinition.Ignores = projectIgnores
          ProjectDefinition.Outputs = projectOutputs
          ProjectDefinition.Targets = projectTargets
          ProjectDefinition.StepDefinitions = projectStepDefinitions
          ProjectDefinition.Labels = labels }




let read workspaceDir (options: Options) environment labels variables =
    let buildFile = Path.Combine(workspaceDir, "BUILD")
    let buildDocument =
        match Yaml.loadDocument buildFile with
        | Ok doc -> doc
        | Error err -> ConfigException($"Configuration '{buildFile}' is invalid", err) |> raise
    let buildConfig = BuildConfigParser.parse buildDocument environment

    // storage
    let storage =
        buildDocument
        |> Yaml.query "/storage"
        |> Yaml.toOptionalString
        |> Option.bind (fun x -> if options.NoCache then None else Some x)
        |> ExtensionLoaders.loadStorage

    // source control
    let sourceControl =
        if options.CI then
            buildDocument
            |> Yaml.query "/sourcecontrol"
            |> Yaml.toOptionalString
            |> ExtensionLoaders.loadSourceControl
        else
            ExtensionLoaders.loadSourceControl None

    let commit = sourceControl.HeadCommit
    let branchOrTag = sourceControl.BranchOrTag

    let defaultExtensions =
        let context = { new Extensions.IContext
                        with member _.Directory = workspaceDir
                             member _.With = None
                             member _.CI = options.CI }

        Map [ "shell", {| Extension = ExtensionLoaders.loadExtension "shell" context
                          Parameters = Map.empty
                          Container = None |}
              "echo", {| Extension = ExtensionLoaders.loadExtension "echo" context
                         Parameters = Map.empty
                         Container = None |} ]

    let processedNodes = ConcurrentDictionary<string, bool>()
    let buildVariables =
        buildConfig.Variables
        |> Map.replace variables

    let rec scanDependency projects project =
        let projectId = IO.combinePath workspaceDir project
        let projectDir, projectFile = 
            match projectId with
            | IO.Directory projectDir -> projectDir, "PROJECT"
            | IO.File _ -> ConfigException($"Dependency '{project}' is not a directory", null) |> raise
            | _ -> failwith $"Failed to find project {projectId}"

        // process only unknown dependency
        if processedNodes.TryAdd(project, true) then
            let projectDef = ProjectConfigParser.parse project workspaceDir buildDocument projectDir projectFile defaultExtensions options.CI commit branchOrTag

            // we go depth-first in order to compute node hash right after
            // NOTE: this could lead to a memory usage problem
            let projects: Map<string, ProjectConfig> =
                try
                    scanDependencies projects projectDef.Dependencies
                with
                    ex ->
                        ConfigException($"while processing '{project}'", ex)
                        |> raise


            // check for circular or missing dependencies
            for childDependency in projectDef.Dependencies do
                if projects |> Map.tryFind childDependency |> Option.isNone then
                    ConfigException($"Circular dependencies between {project} and {childDependency}", null)
                    |> raise



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
                            stepArgsType |> Option.ofObj
                            |> Option.map (fun stepArgsType -> Yaml.deserializeType(stepArgsType, YamlNode.Mapping stepParams))
                            |> Option.defaultValue null

                        let cmds =
                            match stepParameters with
                            | null -> []
                            | stepParameters ->
                                stepDef.Extension.BuildStepCommands(stepDef.Command, stepParameters)

                        cmds
                        |> List.map (fun cmd ->
                            { ContaineredCommandLine.Container = stepDef.Container
                              ContaineredCommandLine.Command = cmd.Command
                              ContaineredCommandLine.Arguments = cmd.Arguments
                              ContaineredCommandLine.Cache = cmd.Cache })
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
                    | _ -> ConfigException($"Variable '{varName}' is not defined in environment '{environment}'", null) |> raise)
                |> Map
                |> Map.add "terrabuild_branch_or_tag" (branchOrTag.Replace("/", "-"))

            let dependenciesHash =
                projectDef.Dependencies
                |> Seq.map (fun dependency -> 
                    match projects |> Map.tryFind dependency with
                    | Some project -> project.Hash
                    | _ ->
                        ConfigException($"Circular dependencies between '{project}' and '{dependency}'", null)
                        |> raise
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
                            | _ -> ConfigException($"Variable {varName} is not defined in \"{environment}\"", null) |> raise)
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
                { ProjectConfig.Dependencies = projectDef.Dependencies
                  ProjectConfig.Files = files
                  ProjectConfig.Outputs = projectDef.Outputs
                  ProjectConfig.Ignores = projectDef.Ignores
                  ProjectConfig.Targets = projectDef.Targets
                  ProjectConfig.Steps = projectSteps
                  ProjectConfig.Hash = nodeHash
                  ProjectConfig.Variables = variables
                  ProjectConfig.Labels = projectDef.Labels }

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
