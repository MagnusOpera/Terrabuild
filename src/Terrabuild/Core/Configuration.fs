module Configuration
open System.IO
open Collections
open System
open System.Collections.Concurrent
open System.Reflection
open Terrabuild.Extensibility

type ExtensionConfig = {
    Version: string option
    Container: YamlNodeValue<string>
    Parameters: Map<string, YamlNode>
}

type Variables = Map<string, string>

type BuildConfig = {
    Storage: string option
    SourceControl: string option
    NuGets: string option
    Environments: Map<string, Variables>
    Targets: Map<string, string set>
    Extensions: Map<string, ExtensionConfig option>
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
    Cache: Cacheability
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
    // open Extensions

//     let loadExtension (container: IContainer) name context : IBuilder =
//         try
//             let factory = container.Resolve<IExtension>(name)
//             factory.CreateBuilder(context)
//         with
//             ex -> failwith $"Plugin '{name}' not found (is it declared in WORKSPACE?): {ex}"

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

    let runStep (extensions: Map<string, Assembly>) (step: Terrabuild.Parser.Build.AST.Step) =
        match extensions |> Map.tryFind step.Extension with
        | None -> failwith $"Unknown extension {step.Extension}"
        | Some assembly ->
            let tpe = assembly.GetType("Script")
            let f = tpe.GetMethod(step.Command)
            let fArgs: obj array =
                f.GetParameters()
                |> Array.map (fun prm ->
                    match step.Parameters |> Map.tryFind prm.Name with
                    | Some paramValue -> paramValue
                    | None -> failwith $"Missing parameter {prm.Name}")
            let r = f.Invoke(null, fArgs) :?> Terrabuild.Extensibility.Step list
            r


module ProjectConfigParser =
    open Terrabuild.Parser.Build.AST

    [<RequireQualifiedAccess>]
    type ProjectDefinition = {
        Dependencies: Items
        Ignores: Items
        Outputs: Items
        Targets: Map<string, Target>
        Labels: string set
        Extensions: Map<string, Assembly>
    }

    let validate (extensions: Map<string, Assembly>) (projectConfig: Terrabuild.Parser.Build.AST.Build) (workspaceDir: string) (projectDir: string) =
        let extensions =
            projectConfig.Extensions
            |> Map.map (fun _ ext ->
                match ext.Script with
                | Some script -> Scripting.loadScript script
                | _ -> failwith "Missing script file")
            |> Map.replace extensions

        let projectInfo =
            match projectConfig.Project.Parser with
            | Some parser ->
                match extensions |> Map.tryFind parser with
                | None -> failwith $"Extension {parser} is not defined"
                | Some assembly ->
                    let tpe = assembly.GetType("Script")
                    let f = tpe.GetMethod("parse")
                    f.Invoke(null, [| projectDir |]) :?> Terrabuild.Extensibility.ProjectInfo |> Some
            | _ -> None

        let mergeOpt optData data =
            projectInfo
            |> Option.map optData
            |> Option.defaultValue Set.empty
            |> Set.union data

        let projectOutputs =
            mergeOpt (fun project -> project.Outputs) projectConfig.Project.Outputs

        let projectIgnores =
            mergeOpt (fun project -> project.Ignores) projectConfig.Project.Ignores

        // convert relative dependencies to absolute dependencies respective to workspaceDirectory
        let projectDependencies =
            mergeOpt (fun project -> project.Dependencies) projectConfig.Project.Dependencies
            |> Set.map (fun dep -> IO.combinePath projectDir dep |> IO.relativePath workspaceDir)

        let labels = projectConfig.Project.Labels
        let projectTargets = projectConfig.Targets

        { ProjectDefinition.Dependencies = projectDependencies
          ProjectDefinition.Ignores = projectIgnores
          ProjectDefinition.Outputs = projectOutputs
          ProjectDefinition.Targets = projectTargets
          ProjectDefinition.Labels = labels
          ProjectDefinition.Extensions = extensions }



let read workspaceDir (options: Options) environment labels variables =
    let workspaceFile = IO.combinePath workspaceDir "WORKSPACE"
    let workspaceContent = File.ReadAllText workspaceFile
    let workspaceConfig = FrontEnd.parseWorkspace workspaceContent

    // variables
    let environments = workspaceConfig.Environments
    let envVariables =
        match environments |> Map.tryFind environment with
        | Some variables -> variables.Variables
        | _ ->
            match environment with
            | "default" -> Map.empty
            | _ -> ConfigException.Raise($"Environment '{environment}' not found")
    let buildVariables =
        variables
        |> Map.replace envVariables

    // storage
    let storage =
        workspaceConfig.Terrabuild.Storage
        |> Option.bind (fun x -> if options.NoCache then None else Some x)
        |> ExtensionLoaders.loadStorage

    // source control
    let sourceControl =
        if options.CI then
            workspaceConfig.Terrabuild.SourceControl
            |> ExtensionLoaders.loadSourceControl
        else
            ExtensionLoaders.loadSourceControl None
    let branchOrTag = sourceControl.BranchOrTag

    let extensions =
        workspaceConfig.Extensions
        |> Map.map (fun _ ext ->
            match ext.Script with
            | Some script -> Scripting.loadScript script
            | _ -> failwith "Missing script file")

    let processedNodes = ConcurrentDictionary<string, bool>()

    let rec scanDependency projects project =
        let projectDir = IO.combinePath workspaceDir project
        // let projectId = IO.combinePath workspaceDir project
        // let projectDir, projectFile = 
        //     match projectId with
        //     | IO.Directory projectDir -> projectDir, "PROJECT"
        //     | IO.File _ -> ConfigException.Raise($"Dependency '{project}' is not a directory")
        //     | _ -> failwith $"Failed to find project {projectId}"

        // process only unknown dependency
        if processedNodes.TryAdd(project, true) then
            let projectFile = IO.combinePath projectDir "BUILD"
            let projectContent = File.ReadAllText projectFile
            let projectConfig = FrontEnd.parseBuild projectContent
            let projectDef = ProjectConfigParser.validate extensions projectConfig workspaceDir projectDir

            // let projectDef = ProjectConfigParser.parse workspaceDir workspaceConfig projectDir projectFile extensions options.CI

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
                projectDef.Targets
                |> Map.map (fun targetId target ->
                    target.Steps
                    |> List.collect (fun stepDef ->
                        let stepParams =
                            stepDef.Parameters
                            |> Map.add "nodeHash" (Terrabuild.Parser.AST.Variable "$terrabuild_node_hash")
                        let stepInfos = ExtensionLoaders.runStep projectDef.Extensions stepDef

                        stepInfos
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
                |> Hash.sha256

            // NOTE: this is the hash (modulo target name) used for reconcialiation across executions
            let nodeHash =
                [ project; filesHash; dependenciesHash ]
                |> String.join "\n"
                |> Hash.sha256

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
                        |> Hash.sha256

                    let stepHash =
                        stepWithValues
                        |> Seq.map (fun step -> $"{step.Container} {step.Command} {step.Arguments}")
                        |> String.join "\n"
                        |> Hash.sha256

                    let hash =
                        [ stepHash; variableHash ]
                        |> String.join "\n"
                        |> Hash.sha256

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
      WorkspaceConfig.Build = workspaceConfig
      WorkspaceConfig.Projects = projects
      WorkspaceConfig.Environment = environment }
