module Configuration
open System.IO
open Collections
open System
open System.Collections.Concurrent
open Terrabuild.Extensibility
open Terrabuild.Parser.AST

// type ExtensionConfig = {
//     Version: string option
//     Container: YamlNodeValue<string>
//     Parameters: Map<string, YamlNode>
// }

type Variables = Map<string, string>

// type BuildConfig = {
//     Storage: string option
//     SourceControl: string option
//     Environments: Map<string, Variables>
//     Targets: Map<string, string set>
// }

// type BuilderConfig = {
//     Use: string option
//     With: string option
//     Container: YamlNodeValue<string>
//     Parameters: Map<string, YamlNode>
// }

// type Items = string set
// type CommandConfig = Map<string, YamlNode>
// type TargetRules = string set

// type ProjectConfig = {
//     Builders: Map<string, BuilderConfig option>
//     Dependencies: Items
//     Outputs: Items
//     Ignores: Items
//     Targets: Map<string, Items>
//     Steps: Map<string, CommandConfig list>
//     Labels: Items
// }

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
type ContaineredAction = {
    Container: string option
    Command: string
    Arguments: string
    Cache: Cacheability
}

[<RequireQualifiedAccess>]
type ContaineredTarget = {
    Hash: string
    Variables: Map<string, string>
    DependsOn: string set
    Actions: ContaineredAction list
}

[<RequireQualifiedAccess>]
type Project = {
    Dependencies: string set
    Files: string set
    Ignores: string set
    Outputs: string set
    Targets: Map<string, ContaineredTarget>
    Hash: string
    Labels: string set
}

[<RequireQualifiedAccess>]
type WorkspaceConfig = {
    Storage: Storages.Storage
    SourceControl: SourceControls.SourceControl
    Directory: string
    Dependencies: string set
    Projects: Map<string, Project>
    Environment: string
}

module ExtensionLoaders =
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
    open Terrabuild.Parser.Build.AST

    [<RequireQualifiedAccess>]
    type ProjectDefinition = {
        Dependencies: string set
        Ignores: string set
        Outputs: string set
        Targets: Map<string, Target>
        Labels: string set
        Extensions: Map<string, Extension>
    }

    let explore (extensions: Map<string, Extension>) (projectConfig: Terrabuild.Parser.Build.AST.Build) (workspaceDir: string) (projectDir: string) =
        let extensions =
            extensions
            |> Map.addMap projectConfig.Extensions

        let projectInfo =
            match projectConfig.Project.Parser with
            | Some parser ->
                match extensions |> Map.tryFind parser with
                | None -> failwith $"Extension {parser} is not defined"
                | Some extension ->
                    match extension.Script with
                    | None -> failwith $"Script missing for extension {parser}"
                    | Some script ->
                        let assembly = Scripting.loadScript script
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
        |> Map.map (fun key value -> String value)
        |> Map.addMap envVariables

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

    let processedNodes = ConcurrentDictionary<string, bool>()

    let rec scanDependency projects project =
        let projectDir = IO.combinePath workspaceDir project

        // process only unknown dependency
        if processedNodes.TryAdd(project, true) then
            let projectFile = IO.combinePath projectDir "BUILD"
            let projectContent = File.ReadAllText projectFile
            let projectConfig = FrontEnd.parseBuild projectContent
            let projectDef = ProjectConfigParser.explore workspaceConfig.Extensions projectConfig workspaceDir projectDir

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

            let buildVariables =
                buildVariables
                |> Map.add "terrabuild_node_hash" (String nodeHash)

            let projectSteps =
                projectDef.Targets
                |> Map.map (fun targetName target ->
                    let variables, actions =
                        target.Steps
                        |> List.fold (fun (variables, actions) step ->
                            let extension =
                                match projectDef.Extensions |> Map.tryFind step.Extension with
                                | None -> failwith $"Extension {step.Extension} is not defined"
                                | Some extension -> extension

                            let assembly = Scripting.loadScript extension.Script.Value // NOTE: Script is always Some
                            let tpe = assembly.GetType("Script")
                            let f = tpe.GetMethod(step.Command)
                            let stepVars: Map<string, string> = Map.empty
                            let args: obj array = Array.zeroCreate 0 // TODO: compute parameter values using buildVariables and function requirements
                            let stepActions =
                                f.Invoke(null, args) :?> Terrabuild.Extensibility.Action list
                                |> List.map (fun action -> { ContaineredAction.Container = extension.Container
                                                             ContaineredAction.Command = action.Command
                                                             ContaineredAction.Arguments = action.Arguments
                                                             ContaineredAction.Cache = action.Cache })

                            let variables =
                                variables
                                |> Map.addMap stepVars

                            let actions = actions @ stepActions

                            variables, actions
                        ) (Map.empty, [])

                    let variableHash =
                        variables
                        |> Seq.map (fun kvp -> $"{kvp.Key} = {kvp.Value}")
                        |> String.join "\n"
                        |> Hash.sha256

                    let stepHash =
                        actions
                        |> Seq.map (fun step -> $"{step.Container} {step.Command} {step.Arguments}")
                        |> String.join "\n"
                        |> Hash.sha256

                    let hash =
                        [ stepHash; variableHash ]
                        |> String.join "\n"
                        |> Hash.sha256

                    let dependsOn =
                        match target.DependsOn with
                        | Some dependsOn -> dependsOn
                        | None ->
                            match workspaceConfig.Targets |> Map.tryFind targetName with
                            | Some target -> target.DependsOn
                            | None -> Set.empty

                    { ContaineredTarget.Hash = hash
                      ContaineredTarget.Variables = variables
                      ContaineredTarget.Actions = actions
                      ContaineredTarget.DependsOn = dependsOn }
                )

            let projectConfig =
                { Project.Dependencies = projectDef.Dependencies
                  Project.Files = files
                  Project.Outputs = projectDef.Outputs
                  Project.Ignores = projectDef.Ignores
                  Project.Targets = projectSteps
                  Project.Hash = nodeHash
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
      WorkspaceConfig.Projects = projects
      WorkspaceConfig.Environment = environment }
