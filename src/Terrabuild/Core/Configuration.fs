module Configuration
open System.IO
open Collections
open System
open System.Collections.Concurrent
open Terrabuild.Extensibility
open Terrabuild.Configuration.AST
open Terrabuild.Expressions
open Terrabuild.Configuration.Project.AST

[<RequireQualifiedAccess>]
type Options = {
    WhatIf: bool
    Debug: bool
    MaxConcurrency: int
    Force: bool
    Local: bool
    Retry: bool
    StartedAt: DateTime
}

type ConfigException(msg, ?innerException: Exception) =
    inherit Exception(msg, innerException |> Option.defaultValue null)

    static member Raise(msg, ?innerException) =
        ConfigException(msg, ?innerException=innerException) |> raise


type BulkContext = {
    Script: Terrabuild.Scripting.Script
    Command: string
    Context: Value
}



[<RequireQualifiedAccess>]
type ContaineredActionBatch = {
    BulkContext: BulkContext option

    Cache: Cacheability
    Container: string option
    Actions: Action list
}


[<RequireQualifiedAccess>]
type ContaineredTarget = {
    Hash: string
    Variables: Map<string, string>
    DependsOn: string set
    Outputs: string set
    Actions: ContaineredActionBatch list
}

[<RequireQualifiedAccess>]
type Project = {
    Id: string
    Hash: string
    Dependencies: string set
    Files: string set
    Targets: Map<string, ContaineredTarget>
    Labels: string set
}

[<RequireQualifiedAccess>]
type WorkspaceConfig = {
    Storage: Storages.Storage
    SourceControl: SourceControls.SourceControl
    Dependencies: string set
    Targets: Map<string, Terrabuild.Configuration.Workspace.AST.Target>
    Projects: Map<string, Project>
    Environment: string
}



[<RequireQualifiedAccess>]
type private ProjectDefinition = {
    Dependencies: string set
    Ignores: string set
    Outputs: string set
    Targets: Map<string, Terrabuild.Configuration.Project.AST.Target>
    Labels: string set
    Extensions: Map<string, Terrabuild.Configuration.AST.Extension>
    Scripts: Map<string, Lazy<Terrabuild.Scripting.Script>>
}




let read (options: Options) environment labels variables =
    let workspaceContent = File.ReadAllText "WORKSPACE"
    let workspaceConfig =
        try
            Terrabuild.Configuration.FrontEnd.parseWorkspace workspaceContent
        with exn ->
            ConfigException.Raise("Failed to read WORKSPACE configuration file", exn)

    let workspaceDir = Environment.CurrentDirectory
    IO.createDirectory ".terrabuild"

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

    let sourceControl = SourceControls.Factory.create options.Local
    let storage = Storages.Factory.create()

    if options.Force then
        $" {Ansi.Styles.yellow}{Ansi.Emojis.bang}{Ansi.Styles.reset} force build requested" |> Terminal.writeLine
    if options.Local then
        $" {Ansi.Styles.yellow}{Ansi.Emojis.bang}{Ansi.Styles.reset} local mode requested" |> Terminal.writeLine

    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} source control is {sourceControl.Name}" |> Terminal.writeLine
    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} cache is {storage.Name}" |> Terminal.writeLine

    let branchOrTag = sourceControl.BranchOrTag

    let processedNodes = ConcurrentDictionary<string, bool>()

    let extensions = 
        Extensions.systemExtensions
        |> Map.addMap workspaceConfig.Extensions

    let scripts =
        extensions
        |> Map.map Extensions.lazyLoadScript

    let rec scanDependency projects project =
        let projectDir = project

        // process only unknown dependency
        if processedNodes.TryAdd(project, true) then
            let projectFile = IO.combinePath projectDir "PROJECT"
            let projectContent = File.ReadAllText projectFile
            let projectConfig =
                try
                    Terrabuild.Configuration.FrontEnd.parseProject projectContent
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
                    |> Map.addMap (projectConfig.Extensions |> Map.map Extensions.lazyLoadScript)

                let projectInfo =
                    match projectConfig.Configuration.Init with
                    | Some init ->
                        let parseContext = 
                            let context = { Terrabuild.Extensibility.InitContext.Debug = options.Debug
                                            Terrabuild.Extensibility.InitContext.Directory = projectDir
                                            Terrabuild.Extensibility.InitContext.CI = sourceControl.CI }
                            Value.Map (Map [ "context", Value.Object context ])
                        
                        let result =
                            Extensions.getScript init scripts
                            |> Extensions.invokeScriptMethod<ProjectInfo> "__init__" parseContext

                        match result with
                        | Extensions.Success result -> result
                        | Extensions.ScriptNotFound -> ConfigException.Raise $"Script {init} was not found"
                        | Extensions.TargetNotFound -> ProjectInfo.Default // NOTE: if __init__ is not found - this will silently use default configuration, probably emit warning
                        | Extensions.ErrorTarget exn -> ConfigException.Raise $"Invocation failure of __init__ of script {init}" exn
                    | _ -> ProjectInfo.Default

                let projectInfo = {
                    projectInfo
                    with Ignores = projectConfig.Configuration.Ignores |> Option.defaultValue projectInfo.Ignores
                         Outputs = projectConfig.Configuration.Outputs |> Option.defaultValue projectInfo.Outputs
                         Dependencies = projectConfig.Configuration.Dependencies |> Option.defaultValue projectInfo.Dependencies }

                let labels = projectConfig.Configuration.Labels

                let projectOutputs = projectInfo.Outputs
                let projectIgnores = projectInfo.Ignores
                // convert relative dependencies to absolute dependencies respective to workspaceDirectory
                let projectDependencies =
                    projectInfo.Dependencies
                    |> Set.map (fun dep -> IO.combinePath projectDir dep |> IO.relativePath workspaceDir)

                let projectTargets = projectConfig.Targets

                { ProjectDefinition.Dependencies = projectDependencies
                  ProjectDefinition.Ignores = projectIgnores
                  ProjectDefinition.Outputs = projectOutputs
                  ProjectDefinition.Targets = projectTargets
                  ProjectDefinition.Labels = labels
                  ProjectDefinition.Extensions = extensions
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
            let projectHash =
                [ project; filesHash; dependenciesHash ]
                |> String.join "\n"
                |> Hash.sha256

            let parseContext = 
                let context = { Terrabuild.Extensibility.InitContext.Debug = options.Debug
                                Terrabuild.Extensibility.InitContext.Directory = projectDir
                                Terrabuild.Extensibility.InitContext.CI = sourceControl.CI }
                Value.Map (Map [ "context", Value.Object context ])

            let projectSteps =
                projectDef.Targets
                |> Map.map (fun targetName target ->
                    let (variables, outputs), actions =
                        target.Steps
                        |> List.fold (fun ((variables, outputs), actions) step ->
                            let stepVars: Map<string, string> = Map.empty

                            let extension = 
                                match projectDef.Extensions |> Map.tryFind step.Extension with
                                | Some extension -> extension
                                | _ -> ConfigException.Raise $"Extension {step.Extension} is not defined"

                            let stepActions, outputs =
                                let actionContext = { Terrabuild.Extensibility.ActionContext.Debug = options.Debug
                                                      Terrabuild.Extensibility.ActionContext.Directory = projectDir
                                                      Terrabuild.Extensibility.ActionContext.CI = sourceControl.CI
                                                      Terrabuild.Extensibility.ActionContext.NodeHash = projectHash
                                                      Terrabuild.Extensibility.ActionContext.Command = step.Command
                                                      Terrabuild.Extensibility.ActionContext.BranchOrTag = branchOrTag }

                                let actionVariables =
                                    buildVariables
                                    |> Map.add "terrabuild_project" project
                                    |> Map.add "terrabuild_target" targetName

                                let actionContext =
                                    extension.Defaults
                                    |> Map.addMap step.Parameters
                                    |> Map.add "context" (Expr.Object actionContext)
                                    |> Expr.Map
                                    |> Eval.eval actionVariables

                                let script = Extensions.getScript step.Extension projectDef.Scripts
                                let actionGroup =
                                    let result =
                                        script
                                        |> Extensions.invokeScriptMethod<Terrabuild.Extensibility.ActionBatch> step.Command actionContext
                                    match result with
                                    | Extensions.Success result -> result
                                    | Extensions.ScriptNotFound -> ConfigException.Raise $"Script {step.Extension} was not found"
                                    | Extensions.TargetNotFound -> ConfigException.Raise $"Script {step.Extension} has no function {step.Command}"
                                    | Extensions.ErrorTarget exn -> ConfigException.Raise $"Invocation failure of {step.Command} of script {step.Extension}" exn

                                let bulkContext =
                                    if actionGroup.Bulkable then
                                        Some { Script = script.Value
                                               Command = step.Command
                                               Context = actionContext }
                                    else
                                        None

                                let containedActionBatch = {
                                    ContaineredActionBatch.BulkContext = bulkContext
                                    ContaineredActionBatch.Container = extension.Container
                                    ContaineredActionBatch.Cache = actionGroup.Cache
                                    ContaineredActionBatch.Actions = actionGroup.Actions
                                }

                                let newOutputs =
                                    let init = step.Extension
                                    let result =
                                        Extensions.getScript init scripts
                                        |> Extensions.invokeScriptMethod<ProjectInfo> "__init__" parseContext
                                    match result with
                                    | Extensions.Success result -> result.Outputs
                                    | Extensions.ScriptNotFound -> ConfigException.Raise $"Script {init} was not found"
                                    | Extensions.TargetNotFound -> Set.empty // NOTE: if __init__ is not found - this will silently use default configuration, probably emit warning
                                    | Extensions.ErrorTarget exn -> ConfigException.Raise $"Invocation failure of __init__ of script {init}" exn

                                containedActionBatch, (outputs + newOutputs)

                            let variables =
                                variables
                                |> Map.addMap stepVars

                            let actions = actions @ [ stepActions ]

                            (variables, outputs), actions
                        ) ((Map.empty, projectDef.Outputs), [])

                    let variableHash =
                        variables
                        |> Seq.map (fun kvp -> $"{kvp.Key} = {kvp.Value}")
                        |> String.join "\n"
                        |> Hash.sha256

                    let stepHash =
                        actions
                        |> Seq.collect (fun batch ->
                            batch.Actions |> Seq.map(fun step ->
                                $"{batch.Container} {step.Command} {step.Arguments}"))
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
                      ContaineredTarget.DependsOn = dependsOn
                      ContaineredTarget.Outputs = outputs }
                )

            let files =
                files
                |> Set.map (IO.relativePath projectDir)

            let projectConfig =
                { Project.Id = project
                  Project.Hash = projectHash
                  Project.Dependencies = projectDef.Dependencies
                  Project.Files = files
                  Project.Targets = projectSteps
                  Project.Labels = projectDef.Labels }

            projects |> Map.add project projectConfig
        else
            projects

    and scanDependencies projects dependencies =
        dependencies |> Seq.fold scanDependency projects

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
        "."
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

    { WorkspaceConfig.Dependencies = dependencies
      WorkspaceConfig.Projects = projects
      WorkspaceConfig.Targets = workspaceConfig.Targets
      WorkspaceConfig.Environment = environment
      WorkspaceConfig.SourceControl = sourceControl
      WorkspaceConfig.Storage = storage }
