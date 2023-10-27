module Configuration
open System.IO
open Collections
open System
open System.Collections.Concurrent





type ConfigException(msg, innerException: Exception) =
    inherit Exception(msg, innerException)



type Dependencies = Set<string>
type Paths = Set<string>
type TargetRules = Set<string>
type Targets = Map<string, TargetRules>
type StepCommands = Extensions.CommandLine list
type Steps = Map<string, StepCommands>
type Variables = Map<string, string>
type ExtensionConfigs = Map<string, Map<string, string>>



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
        | _ -> failwith $"Unknown plugin {name}"

    let loadStorage name : Storages.Storage =
        match name with
        | None -> Storages.Local()
        | Some "azureblob" -> Storages.MicrosoftBlobStorage()
        | _ -> failwith $"Unknown storage {name}"

    let loadSourceControl name: SourceControls.SourceControl =
        match name with
        | None -> SourceControls.Local()
        | Some "github" -> SourceControls.GitHub()
        | _ -> failwith $"Unknown source control {name}"

module BuildConfigParser =

    [<RequireQualifiedAccess>]
    type BuildConfig = {
        Targets: Targets
        Variables: Variables
    }


    let parse buildDocument environment =
        // targets
        let targets =
            match buildDocument |> Yaml.query "/targets" with
            | Some (Yaml.Mapping (_, mapping)) -> mapping |> Map.map (fun _ -> Set << Yaml.toStringList)
            | _ -> Map.empty

        // variables
        let environments =
            match buildDocument |> Yaml.query "/environments" with
            | Some (Yaml.Mapping (_, mapping)) -> mapping |> Map.map (fun _ -> Yaml.toStringMap)
            | Some _ -> failwith "Invalid configuration for environements"
            | None -> Map.empty
        let variables =
            match environments |> Map.tryFind environment with
            | Some variables -> variables
            | _ ->
                match environment with
                | "default" -> Map.empty
                | _ ->
                    ConfigException($"Environment {environment} not found", null)
                    |> raise

        let buildConfig = { BuildConfig.Targets = targets
                            BuildConfig.Variables = variables }
        buildConfig






module ProjectConfigParser =

    [<RequireQualifiedAccess>]
    type StepDefinition = {
        Extension: Extensions.Extension
        Command: string
        Parameters: string
    }

    [<RequireQualifiedAccess>]
    type ProjectDefinition = {
        Dependencies: Dependencies
        Ignores: Paths
        Outputs: Paths
        Targets: Targets
        StepDefinitions: Map<string, StepDefinition list>
        Labels: Set<string>
    }

    let getExtensionFromInvocation name =
        match name with
        | String.Regex "^\((\w+)\)$" [name] -> Some name
        | _ -> None

    let parse projectId workspaceDir buildDocument projectDir projectFile defaultExtensions shared commit branchOrTag =
        // we might have landed in a directory without a configuration
        // in that case we just use the default configuration (which does nothing)
        let projectDocument =
            match IO.combinePath projectDir projectFile with
            | IO.File projectFile -> Yaml.loadDocument projectFile
            | _ -> null

        let projectOutputs =
            projectDocument
            |> Yaml.query "/outputs"
            |> Yaml.toOptionalStringList
            |> Set

        let projectIgnores =
            projectDocument
            |> Yaml.query "/outputs"
            |> Yaml.toOptionalStringList
            |> Set

        let projectTargets =
            match projectDocument |> Yaml.query "/targets" with
            | Some (Yaml.Mapping (_, mapping)) -> mapping |> Map.map (fun _ -> Set << Yaml.toStringList)
            | _ -> Map.empty

        let projectDependencies =
            projectDocument
            |> Yaml.query "/dependencies"
            |> Yaml.toOptionalStringList
            |> Set

        let projectBuilders =
            match projectDocument |> Yaml.query "/builders" with
            | Some (Yaml.Mapping (_, builderMappings)) ->
                builderMappings |> Map.map (fun alias mapping ->
                    let builderUse =
                        mapping |> Yaml.query "use" |> Yaml.toOptionalString |> Option.defaultValue alias
                    let builderWith =
                        mapping |> Yaml.query "with" |> Yaml.toOptionalString
                    let builderParams =
                        let configBuilderParams =
                            match buildDocument |> Yaml.query $"/extensions/{builderUse}" with
                            | Some (Yaml.Mapping (_, mapping)) -> mapping
                            | Some _ -> failwith "Expecting mapping"
                            | _ -> Map.empty

                        let configProjectParams =
                            match mapping |> Yaml.query $"parameters" with
                            | Some (Yaml.Mapping (_, mapping)) -> mapping
                            | Some _ -> failwith "Expecting mapping"
                            | _ -> Map.empty

                        let builderParams = configBuilderParams |> Map.replace configProjectParams
                        builderParams
 
                    let context = { new Extensions.IContext
                                    with member _.Directory = projectDir
                                         member _.With = builderWith
                                         member _.Shared = shared
                                         member _.Commit = commit
                                         member _.BranchOrTag = branchOrTag }
  
                    let builder = ExtensionLoaders.loadExtension builderUse context 
                    builder, builderParams)
            | Some _ -> ConfigException($"Project {projectId} has malformed builders mapping", null) |> raise
            | _ -> Map.empty

        // collect extension capabilities
        let builderOutputs =
            projectBuilders
            |> Seq.collect (fun (KeyValue(_, (extension, _))) -> extension.Outputs)
            |> Set

        let builderIgnores =
            projectBuilders
            |> Seq.collect (fun (KeyValue(_, (extension, _))) -> extension.Ignores)
            |> Set

        let builderDependencies =
            projectBuilders
            |> Seq.collect (fun (KeyValue(_, (extension, _))) -> extension.Dependencies)
            |> Set

        let projectOutputs = projectOutputs + builderOutputs
        let projectIgnores = projectIgnores + builderIgnores


        // convert relative dependencies to absolute dependencies respective to workspaceDirectory
        let projectDependencies =
            (projectDependencies + builderDependencies)
            |> Set.map (fun dep -> IO.combinePath projectDir dep
                                    |> IO.relativePath workspaceDir)

        let labels =
            match projectDocument |> Yaml.query "/labels" with
            | Some (Yaml.Sequence (_, sequence)) -> sequence |> List.map Yaml.toString |> Set
            | _ -> Set.empty

        let projectBuilders = defaultExtensions |> Map.replace projectBuilders

        let projectStepDefinitions =
            match projectDocument |> Yaml.query "/steps" with
            | Some (Yaml.Mapping (_, stepMappings)) ->
                stepMappings |> Map.map (fun _ stepMapping ->
                    match stepMapping with
                    | Yaml.Sequence (_, actions) ->
                        actions |> List.map (fun action ->
                            match action with
                            | Yaml.Mapping (_, actionConfig) ->
                                let builderInfo, stepParams = actionConfig |> Map.partition (fun k _ -> k |> getExtensionFromInvocation |> Option.isSome)
                                let builderInfo = builderInfo |> Seq.exactlyOne
                                let builderName, builderCommand = builderInfo.Key |> getExtensionFromInvocation |> Option.get, builderInfo.Value |> Yaml.toString
                                let (builder, builderParams) = projectBuilders |> Map.find builderName

                                let stepParams =
                                    builderParams
                                    |> Map.replace stepParams 
                                    |> Seq.map (fun kvp -> System.Collections.Generic.KeyValuePair(YamlDotNet.RepresentationModel.YamlScalarNode(kvp.Key) :> YamlDotNet.RepresentationModel.YamlNode, kvp.Value))
                                    |> YamlDotNet.RepresentationModel.YamlMappingNode

                                use writer = new StringWriter()
                                YamlDotNet.RepresentationModel.YamlStream([
                                    if stepParams.Children.Count > 0 then YamlDotNet.RepresentationModel.YamlDocument(stepParams)
                                ]).Save(writer)
                                let content = writer.ToString()

                                { StepDefinition.Extension = builder
                                  StepDefinition.Command = builderCommand
                                  StepDefinition.Parameters = content }
                            | _ -> failwith "Expecting mapping")
                    | _ -> failwith "Expecting sequence")
            | _ -> Map.empty

        { ProjectDefinition.Dependencies = projectDependencies
          ProjectDefinition.Ignores = projectIgnores
          ProjectDefinition.Outputs = projectOutputs
          ProjectDefinition.Targets = projectTargets
          ProjectDefinition.StepDefinitions = projectStepDefinitions
          ProjectDefinition.Labels = labels }



type ProjectConfig = {
    Dependencies: Dependencies
    Ignores: Paths
    Outputs: Paths
    Targets: Targets
    Steps: Steps
    Hash: string
    Variables: Variables
    Labels: Set<string>
}

type WorkspaceConfig = {
    Storage: Storages.Storage
    SourceControl: SourceControls.SourceControl
    Directory: string
    Dependencies: Dependencies
    Build: BuildConfigParser.BuildConfig
    Projects: Map<string, ProjectConfig>
    Environment: string
}



let read workspaceDir shared environment labels variables =
    let buildFile = Path.Combine(workspaceDir, "BUILD")
    let buildDocument = Yaml.loadDocument buildFile
    let buildConfig = BuildConfigParser.parse buildDocument environment

    // storage
    let storage =
        if shared then
            buildDocument
            |> Yaml.query "/storage"
            |> Yaml.toOptionalString
            |> ExtensionLoaders.loadStorage 
        else
            ExtensionLoaders.loadStorage None

    // source control
    let sourceControl =
        if shared then
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
                                member _.Shared = shared
                                member _.Commit = commit
                                member _.BranchOrTag = branchOrTag }

        Map [ "shell", (ExtensionLoaders.loadExtension "shell" context, Map.empty)
              "echo", (ExtensionLoaders.loadExtension "echo" context, Map.empty) ]

    let processedNodes = ConcurrentDictionary<string, bool>()
    let buildVariables =
        buildConfig.Variables
        |> Map.replace variables

    let rec scanDependency projects project =
        let projectId = IO.combinePath workspaceDir project
        let projectDir, projectFile = 
            match projectId with
            | IO.Directory projectDir -> projectDir, "PROJECT"
            | IO.File _ -> ConfigException($"Dependency {project} is not a directory", null) |> raise
            | _ -> failwith $"Failed to find project {projectId}"

        // process only unknown dependency
        if processedNodes.TryAdd(project, true) then
            let projectDef = ProjectConfigParser.parse project workspaceDir buildDocument projectDir projectFile defaultExtensions shared commit branchOrTag

            // we go depth-first in order to compute node hash right after
            // NOTE: this could lead to a memory usage problem
            let projects =
                try
                    scanDependencies projects projectDef.Dependencies
                with
                    ex ->
                        ConfigException($"while processing {project}", ex)
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

            let variables = 
                projectDef.StepDefinitions
                |> Seq.collect (fun l -> l.Value)
                |> Seq.collect (fun stepDef -> String.AllMatches "\$\((\w+)\)" stepDef.Parameters)
                |> Set
                |> Seq.map (fun varName ->
                    match buildVariables |> Map.tryFind varName with
                    | Some value -> varName, value
                    | _ -> ConfigException($"Variable {varName} is not defined in \"{environment}\"", null) |> raise)
                |> Map

            let dependenciesHash =
                projectDef.Dependencies
                |> Seq.map (fun dependency -> 
                    match projects |> Map.tryFind dependency with
                    | Some project -> project.Hash
                    | _ ->
                        ConfigException($"Circular dependencies between {project} and {dependency}", null)
                        |> raise
                )
                |> Seq.sort
                |> String.join "\n"
                |> String.sha256

            let variableHashes =
                variables
                |> Seq.map (fun kvp -> $"{kvp.Key} = {kvp.Value}")
                |> String.join "\n"
                |> String.sha256

            // NOTE: this is the hash (modulo target name) used for reconcialiation across executions
            let nodeHash =
                [ project; filesHash; dependenciesHash; variableHashes ]
                |> String.join "\n"
                |> String.sha256

            let projectSteps =
                projectDef.StepDefinitions
                |> Map.map (fun targetId steps ->
                    steps
                    |> List.collect (fun stepDef ->
                        let stepParams = $"nodeHash: \"{nodeHash}\"\n{stepDef.Parameters}"

                        let variables =
                            variables
                            |> Map.add "terrabuild_node_hash" nodeHash

                        let stepParams =
                            variables
                            |> Map.fold (fun acc key value -> acc |> String.replace $"$({key})" value) stepParams
                        let stepArgsType =
                            stepDef.Extension.GetStepParameters stepDef.Command
                        let stepParameters =
                                stepArgsType |> Option.ofObj
                                |> Option.map (fun stepArgsType -> stepParams |> Yaml.loadModelFromType stepArgsType)
                                |> Option.defaultValue null

                        match stepParameters with
                        | null -> []
                        | :? Extensions.StepParameters as stepParameters ->
                            stepDef.Extension.BuildStepCommands(stepDef.Command, stepParameters)
                        | _ -> failwith "Unexpected type for action type"

                        // match container with
                        // | Some container ->
                        //     let commands =
                        //         commands
                        //         |> List.map (fun command ->
                        //             { WorkspaceCommandLine.WorkingDir = workspaceDir
                        //               WorkspaceCommandLine.Command = "docker"
                        //               WorkspaceCommandLine.Arguments = $"run --rm -v {IO.combinePath Environment.CurrentDirectory projectDir}:/terrabuild -w /terrabuild {container} {command.Command} {command.Arguments}" })
                        //     commands
                        // | None ->
                        //     let commands =
                        //         commands
                        //         |> List.map (fun command ->
                        //             { WorkspaceCommandLine.WorkingDir = projectDir
                        //               WorkspaceCommandLine.Command = command.Command
                        //               WorkspaceCommandLine.Arguments = command.Arguments })
                        //     commands
                    )
                )


            let projectConfig =
                { Dependencies = projectDef.Dependencies
                  Outputs = projectDef.Outputs
                  Ignores = projectDef.Ignores
                  Targets = projectDef.Targets
                  Steps = projectSteps
                  Hash = nodeHash
                  Variables = variables
                  Labels = projectDef.Labels }

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

    { Directory = workspaceDir
      Dependencies = dependencies
      Storage = storage
      SourceControl = sourceControl
      Build = buildConfig
      Projects = projects
      Environment = environment }
