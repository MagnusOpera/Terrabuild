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
type StepCommands = Extensions.Command list
type Steps = Map<string, StepCommands>
type Variables = Map<string, string>
type ExtensionConfigs = Map<string, Map<string, string>>



module ExtensionLoaders =

    let loadExtension name projectDir projectFile : Extensions.Extension =
        let context = { new Extensions.IContext
                        with member _.Directory = projectDir
                             member _.With = projectFile }

        match name with
        | "dotnet" -> Extensions.Dotnet(context)
        | "shell" -> Extensions.Shell(context)
        | "docker" -> Extensions.Docker(context)
        | "make" -> Extensions.Make(context)
        | "echo" -> Extensions.Echo(context)
        | _ -> failwith $"Unknown plugin {name}"

    let loadStorage name : Storages.Storage =
        match name with
        | "azureblob" -> Storages.MicrosoftBlobStorage() :> Storages.Storage
        | _ -> failwith $"Unknown storage {name}"



module BuildConfigParser =

    [<RequireQualifiedAccess>]
    type BuildConfig = {
        Storage: Storages.Storage option
        Dependencies: Dependencies
        Targets: Targets
        Variables: Variables
    }


    let parse buildDocument shared environment =
        // storage
        let storage =
            if shared then
                buildDocument
                |> Yaml.query "/storage"
                |> Yaml.toOptionalString
                |> Option.map ExtensionLoaders.loadStorage 
            else
                None

        // dependencies
        let dependencies =
            buildDocument
            |> Yaml.query "/dependencies"
            |> Yaml.toOptionalStringList

        // targets
        let targets =
            match buildDocument |> Yaml.query "/targets" with
            | Some (Yaml.Mapping (_, mapping)) -> mapping |> Map.map (fun _ -> Set.ofList << Yaml.toStringList)
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

        let buildConfig = { BuildConfig.Dependencies = dependencies |> Set.ofSeq
                            BuildConfig.Targets = targets
                            BuildConfig.Variables = variables
                            BuildConfig.Storage = storage}
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
    }

    let getExtensionFromInvocation name =
        match name with
        | String.Regex "^\((\w+)\)$" [name] -> Some name
        | _ -> None

    let parse workspaceDir buildDocument projectDir projectFile =
        let defaultExtensions = 
            Map [ "shell", (ExtensionLoaders.loadExtension "shell" projectDir None, Map.empty)
                  "echo", (ExtensionLoaders.loadExtension "echo" projectDir None, Map.empty) ]

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
            |> Set.ofList

        let projectIgnores =
            projectDocument
            |> Yaml.query "/outputs"
            |> Yaml.toOptionalStringList
            |> Set.ofList

        let projectTargets =
            match projectDocument |> Yaml.query "/targets" with
            | Some (Yaml.Mapping (_, mapping)) -> mapping |> Map.map (fun _ -> Set.ofList << Yaml.toStringList)
            | _ -> Map.empty

        let projectDependencies =
            projectDocument
            |> Yaml.query "/dependencies"
            |> Yaml.toOptionalStringList
            |> Set.ofList

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
                    let builder = ExtensionLoaders.loadExtension builderUse projectDir builderWith
                    builder, builderParams)
            | Some _ -> failwith "Expecting mapping"
            | _ -> Map.empty

        // collect extension capabilities
        let builderOutputs =
            projectBuilders
            |> Seq.collect (fun (KeyValue(_, (extension, _))) -> extension.Outputs)
            |> Set.ofSeq

        let builderIgnores =
            projectBuilders
            |> Seq.collect (fun (KeyValue(_, (extension, _))) -> extension.Ignores)
            |> Set.ofSeq

        let builderDependencies =
            projectBuilders
            |> Seq.collect (fun (KeyValue(_, (extension, _))) -> extension.Dependencies)
            |> Set.ofSeq

        let projectOutputs = projectOutputs + builderOutputs
        let projectIgnores = projectIgnores + projectOutputs + builderIgnores


        // convert relative dependencies to absolute dependencies respective to workspaceDirectory
        let projectDependencies =
            (projectDependencies + builderDependencies)
            |> Set.map (fun dep -> IO.combinePath projectDir dep
                                    |> IO.relativePath workspaceDir)


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
                                let builderInfo, stepParams = actionConfig |> Map.partition (fun k v -> k |> getExtensionFromInvocation |> Option.isSome)
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
                                    YamlDotNet.RepresentationModel.YamlDocument(stepParams)
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
          ProjectDefinition.StepDefinitions = projectStepDefinitions }




type ProjectConfig = {
    Dependencies: Dependencies
    Ignores: Paths
    Outputs: Paths
    Targets: Targets
    Steps: Steps
    Hash: string
    Variables: Variables
}

type WorkspaceConfig = {
    Directory: string
    Build: BuildConfigParser.BuildConfig
    Projects: Map<string, ProjectConfig>
    Environment: string
}



let read workspaceDir shared environment =
    let buildFile = Path.Combine(workspaceDir, "BUILD")
    let buildDocument = Yaml.loadDocument buildFile
    let buildConfig = BuildConfigParser.parse buildDocument shared environment


    let processedNodes = ConcurrentDictionary<string, bool>()
    let mutable projects = Map.empty
    let buildVariables = buildConfig.Variables


    let rec scanDependencies dependencies =
        for project in dependencies do
            let projectId = IO.combinePath workspaceDir project
            let projectDir, projectFile = 
                match projectId with
                | IO.File projectFile -> IO.parentDirectory projectFile, IO.getFilename projectFile
                | IO.Directory projectDir -> projectDir, "PROJECT"
                | _ -> failwith $"Failed to find project {projectId}"

            // process only unknown dependency
            if processedNodes.TryAdd(project, true) then

                let projectDef = ProjectConfigParser.parse workspaceDir buildDocument projectDir projectFile

                // we go depth-first in order to compute node hash right after
                // NOTE: this could lead to a memory usage problem
                try
                    scanDependencies projectDef.Dependencies
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
                let files = projectDir |> IO.enumerateFilesBut projectDef.Ignores |> Set.ofSeq
                let filesHash =
                    files
                    |> Seq.sort
                    |> Hash.computeFilesSha

                let variables = 
                    projectDef.StepDefinitions
                    |> Seq.collect (fun l -> l.Value)
                    |> Seq.collect (fun stepDef -> String.AllMatches "\$\((\w+)\)" stepDef.Parameters)
                    |> Set.ofSeq
                    |> Seq.map (fun varName ->
                        match buildVariables |> Map.tryFind varName with
                        | Some value -> varName, value
                        | _ -> ConfigException($"Variable {varName} is not defined in \"{environment}\"", null) |> raise)
                    |> Map.ofSeq

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
                    [ filesHash; dependenciesHash; variableHashes ]
                    |> String.join "\n"
                    |> String.sha256

                let projectSteps =
                    projectDef.StepDefinitions
                    |> Map.map (fun targetId steps ->
                        let cacheEntryId = $"{projectId}/{nodeHash}/{targetId}"
                        let nodeTargetHash = cacheEntryId |> String.sha256

                        steps
                        |> List.collect (fun stepDef ->

                            let stepParams = $"nodeHash: \"{nodeTargetHash}\"\nshared: {shared}\n{stepDef.Parameters}"

                            let variables =
                                variables
                                |> Map.add "terrabuild_node_hash" nodeTargetHash

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
                        )
                    )


                let projectConfig =
                    { Dependencies = projectDef.Dependencies
                      Outputs = projectDef.Outputs
                      Ignores = projectDef.Ignores
                      Targets = projectDef.Targets
                      Steps = projectSteps
                      Hash = nodeHash
                      Variables = variables }

                projects <- projects |> Map.add project projectConfig


    // initial dependency list is absolute respective to workspaceDirectory
    scanDependencies buildConfig.Dependencies
    { Directory = workspaceDir
      Build = buildConfig
      Projects = projects
      Environment = environment }
