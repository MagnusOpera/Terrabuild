module Configuration
open System.IO
open Collections
open System
open System.Collections.Concurrent
open Terrabuild.Extensibility
open Terrabuild.Parser.AST
open Terrabuild.Expressions

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



[<RequireQualifiedAccess>]
type ProjectDefinition = {
    Dependencies: string set
    Ignores: string set
    Outputs: string set
    Targets: Map<string, Terrabuild.Parser.Project.AST.Target>
    Labels: string set
    Extensions: Map<string, Extension>
    Properties: Map<string, string>
    Scripts: Map<string, Lazy<Terrabuild.Scripting.Script>>
}

module ExtensionLoaders =
    open Terrabuild.Scripting

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

    let terrabuildDir = System.Reflection.Assembly.GetExecutingAssembly().Location |> IO.parentDirectory
    let terrabuildExtensibility = IO.combinePath terrabuildDir "Terrabuild.Extensibility.dll"

    let lazyLoadScript name (ext: Extension) =
        let script =
            match ext.Script with
            | Some script -> script
            | None ->
                let providedScripts = Set [ "@docker"; "@dotnet"; "@npm"; "@make"; "@shell"; "@terraform" ]
                if providedScripts |> Set.contains name then IO.combinePath terrabuildDir $"Scripts/{name}.fsx"
                else failwith $"Script is not defined for extension '{name}'"

        let initScript () =
            let script = loadScript [ terrabuildExtensibility ] script
            script

        lazy(initScript())

    let rec invokeScriptMethod<'r> (scripts: Map<string, Lazy<Script>>) (extension: string) (method: string) (args: Value) =
        try
            match scripts |> Map.tryFind extension with
            | None -> failwith $"Extension '{extension}' is not defined"
            | Some extInstance ->
                let script = extInstance.Value
                let invocable = script.GetMethod(method)
                match invocable with
                | Some invocable -> invocable.Invoke<'r> args
                | None when method <> "__dispatch__" -> invokeScriptMethod scripts extension "__dispatch__" args
                | _ -> failwith "Extension '{extension} does not provide function '{method}'"
        with
        | exn -> ConfigException.Raise($"error while invoking method '{method}' from extension '{extension}'", exn)






let read workspaceDir (options: Options) environment labels variables =
    let workspaceFile = IO.combinePath workspaceDir "WORKSPACE"
    let workspaceContent = File.ReadAllText workspaceFile
    let workspaceConfig =
        try
            FrontEnd.parseWorkspace workspaceContent
        with exn ->
            ConfigException.Raise("Failed to read WORKSPACE configuration file", exn)

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
        |> Map.map (fun _ value -> value)
        |> Map.addMap envVariables

    // storage
    let storage =
        workspaceConfig.Configuration.Storage
        |> Option.bind (fun x -> if options.NoCache then None else Some x)
        |> ExtensionLoaders.loadStorage

    // source control
    let sourceControl =
        if options.CI then
            workspaceConfig.Configuration.SourceControl
            |> ExtensionLoaders.loadSourceControl
        else
            ExtensionLoaders.loadSourceControl None
    let branchOrTag = sourceControl.BranchOrTag

    let processedNodes = ConcurrentDictionary<string, bool>()

    let extensions =
        workspaceConfig.Extensions
        |> Map.add "@shell" Extension.Empty

    let scripts =
        extensions
        |> Map.map ExtensionLoaders.lazyLoadScript

    let rec scanDependency projects project =
        let projectDir = IO.combinePath workspaceDir project

        // process only unknown dependency
        if processedNodes.TryAdd(project, true) then
            let projectFile = IO.combinePath projectDir "PROJECT"
            let projectContent = File.ReadAllText projectFile
            let projectConfig =
                try
                    FrontEnd.parseProject projectContent
                with exn ->
                    ConfigException.Raise($"Failed to read PROJECT configuration {projectFile}", exn)

            let projectDef =
                // NOTE: here we are tracking both extensions (that is configuration) and scripts (compiled extensions)
                // Order is important as we just want to override in the project and reduce as much as possible scripts compilation
                // In other terms: we only compile what's changed
                let extensions =
                    extensions
                    |> Map.addMap projectConfig.Extensions

                let scripts =
                    scripts
                    |> Map.addMap (projectConfig.Extensions |> Map.map ExtensionLoaders.lazyLoadScript)

                let projectInfo =
                    match projectConfig.Configuration.Parser with
                    | Some parser ->
                        let parseContext = 
                            let context = { Terrabuild.Extensibility.InitContext.Directory = projectDir
                                            Terrabuild.Extensibility.InitContext.CI = options.CI }
                            Value.Map (Map [ "context", Value.Object context ])
                        ExtensionLoaders.invokeScriptMethod<Terrabuild.Extensibility.ProjectInfo> scripts parser "__init__" parseContext |> Some
                    | _ -> None

                let mergeOpt optData data =
                    projectInfo
                    |> Option.map optData
                    |> Option.defaultValue Set.empty
                    |> Set.union data

                let projectOutputs =
                    mergeOpt (fun project -> project.Outputs) projectConfig.Configuration.Outputs

                let projectIgnores =
                    mergeOpt (fun project -> project.Ignores) projectConfig.Configuration.Ignores

                // convert relative dependencies to absolute dependencies respective to workspaceDirectory
                let projectDependencies =
                    mergeOpt (fun project -> project.Dependencies) projectConfig.Configuration.Dependencies
                    |> Set.map (fun dep -> IO.combinePath projectDir dep |> IO.relativePath workspaceDir)

                let labels = projectConfig.Configuration.Labels
                let projectTargets = projectConfig.Targets

                let properties = projectInfo |> Option.map (fun pi -> pi.Properties) |> Option.defaultValue Map.empty

                { ProjectDefinition.Dependencies = projectDependencies
                  ProjectDefinition.Ignores = projectIgnores
                  ProjectDefinition.Outputs = projectOutputs
                  ProjectDefinition.Targets = projectTargets
                  ProjectDefinition.Labels = labels
                  ProjectDefinition.Extensions = extensions
                  ProjectDefinition.Properties = properties
                  ProjectDefinition.Scripts = scripts }

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

            let projectSteps =
                projectDef.Targets
                |> Map.map (fun targetName target ->
                    let variables, actions =
                        target.Steps
                        |> List.fold (fun (variables, actions) step ->
                            let stepVars: Map<string, string> = Map.empty

                            let extension = 
                                match projectDef.Extensions |> Map.tryFind step.Extension with
                                | Some extension -> extension
                                | _ -> ConfigException.Raise $"Extension {step.Extension} is not defined"

                            let stepActions =
                                let actionContext = { Terrabuild.Extensibility.ActionContext.Properties = projectDef.Properties
                                                      Terrabuild.Extensibility.ActionContext.Directory = projectDir
                                                      Terrabuild.Extensibility.ActionContext.CI = options.CI
                                                      Terrabuild.Extensibility.ActionContext.NodeHash = nodeHash
                                                      Terrabuild.Extensibility.ActionContext.Command = targetName
                                                      Terrabuild.Extensibility.ActionContext.BranchOrTag = branchOrTag }

                                let stepParameters =
                                    extension.Defaults
                                    |> Map.addMap step.Parameters
                                    |> Map.add "context" (Expr.Object actionContext)
                                    |> Expr.Map
                                    |> Eval.eval buildVariables

                                ExtensionLoaders.invokeScriptMethod<Terrabuild.Extensibility.Action list> projectDef.Scripts
                                                                                                          step.Extension 
                                                                                                          step.Command
                                                                                                          stepParameters
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
