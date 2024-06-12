module Configuration
open System.IO
open Collections
open System
open System.Collections.Concurrent
open Terrabuild.Extensibility
open Terrabuild.Configuration.AST
open Terrabuild.Expressions
open Terrabuild.Configuration.Project.AST
open Errors
open Terrabuild.PubSub

[<RequireQualifiedAccess>]
type Options = {
    WhatIf: bool
    Debug: bool
    MaxConcurrency: int
    Force: bool
    Retry: bool
    StartedAt: DateTime
    IsLog: bool
}

type BatchContext = {
    Script: Terrabuild.Scripting.Script
    Command: string
    Context: Value
}

[<RequireQualifiedAccess>]
type ContaineredActionBatch = {
    Cache: Cacheability
    MetaCommand: string
    Actions: Action list

    Container: string option
    ContainerVariables: string Set
    BatchContext: BatchContext option
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
type Workspace = {
    // Space to use
    Space: string option

    // Source control in use
    SourceControl: Contracts.SourceControl

    // Computed projects selection (derived from user inputs)
    ComputedProjectSelection: string set

    // All targets at workspace level
    Targets: Map<string, Terrabuild.Configuration.Workspace.AST.Target>

    // All discovered projects in workspace
    Projects: Map<string, Project>

    // Configuration provided by user
    Configuration: string

    // Note provided by user
    Note: string option

    // Tag provided by user
    Tag: string option
}


type private LazyScript = Lazy<Terrabuild.Scripting.Script>

[<RequireQualifiedAccess>]
type private LoadedProject = {
    Dependencies: string set
    Includes: string set
    Ignores: string set
    Outputs: string set
    Targets: Map<string, Target>
    Labels: string set
    Extensions: Map<string, Extension>
    Scripts: Map<string, LazyScript>
}


let read workspaceDir configuration note tag labels (variables: Map<string, string>) (sourceControl: Contracts.SourceControl) (options: Options) =
    $"{Ansi.Emojis.box} Reading {configuration} configuration" |> Terminal.writeLine

    let workspaceContent = FS.combinePath workspaceDir "WORKSPACE" |> File.ReadAllText
    let workspaceConfig =
        try
            Terrabuild.Configuration.FrontEnd.parseWorkspace workspaceContent
        with exn ->
            TerrabuildException.Raise("Failed to read WORKSPACE configuration file", exn)

    let convertToVarType (key: string) (expr: Expr) (value: string) =
        match expr with
        | Expr.String _ ->
            Expr.String value
        | Expr.Number _ ->
            match value |> Int32.TryParse with
            | true, value -> Expr.Number value
            | _ -> TerrabuildException.Raise($"Value '{value}' can't be converted to number variable {key}")
        | Expr.Boolean _ ->
            match value |> Boolean.TryParse with
            | true, value -> Expr.Boolean value
            | _ -> TerrabuildException.Raise($"Value '{value}' can't be converted to boolean variable {key}")
        | _ -> TerrabuildException.Raise($"Value 'value' can't be converted to variable {key}")

    // variables
    let configVariables =
        match workspaceConfig.Configurations |> Map.tryFind configuration with
        | Some variables -> variables.Variables
        | _ ->
            match configuration with
            | "default" -> Map.empty
            | _ -> TerrabuildException.Raise($"Configuration '{configuration}' not found")

    let buildVariables =
        configVariables
        // override variable with configuration variable if any
        |> Map.map (fun key expr ->
            match $"TB_VAR_{key |> String.toLower}" |> Environment.GetEnvironmentVariable with
            | null -> expr
            | value -> convertToVarType key expr value)
        // override variable with provided ones on command line if any
        |> Map.map (fun key expr ->
            match variables |> Map.tryFind (key |> String.toLower) with
            | Some value -> convertToVarType key expr value
            | _ -> expr)

    if options.Force then
        $" {Ansi.Styles.yellow}{Ansi.Emojis.bang}{Ansi.Styles.reset} force build requested" |> Terminal.writeLine

    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} source control is {sourceControl.Name}" |> Terminal.writeLine

    let branchOrTag = sourceControl.BranchOrTag

    let extensions = 
        Extensions.systemExtensions
        |> Map.addMap workspaceConfig.Extensions

    let scripts =
        extensions
        |> Map.map (fun _ _ -> None)
        |> Map.map Extensions.lazyLoadScript


    // this is the first stage: load project and mostly get dependencies references
    let loadProjectDef projectId =
        let projectDir = FS.combinePath workspaceDir projectId
        let projectFile = FS.combinePath projectDir "PROJECT"

        let projectContent = File.ReadAllText projectFile
        let projectConfig =
            try Terrabuild.Configuration.FrontEnd.parseProject projectContent
            with exn -> TerrabuildException.Raise($"Failed to read PROJECT configuration {projectFile}", exn)

        // NOTE: here we are tracking both extensions (that is configuration) and scripts (compiled extensions)
        // Order is important as we just want to override in the project and reduce as much as possible scripts compilation
        // In other terms: we only compile what's changed
        let extensions =
            extensions
            |> Map.addMap projectConfig.Extensions

        let projectScripts =
            projectConfig.Extensions
            |> Map.map (fun _ ext -> ext.Script |> Option.map (FS.workspaceRelative workspaceDir projectDir))

        let scripts =
            scripts
            |> Map.addMap (projectScripts |> Map.map Extensions.lazyLoadScript)

        let projectInfo =
            match projectConfig.Project.Init with
            | Some init ->
                let parseContext = 
                    let context = { Terrabuild.Extensibility.ExtensionContext.Debug = options.Debug
                                    Terrabuild.Extensibility.ExtensionContext.Directory = projectDir
                                    Terrabuild.Extensibility.ExtensionContext.CI = sourceControl.CI }
                    Value.Map (Map [ "context", Value.Object context ])
                
                let result =
                    Extensions.getScript init scripts
                    |> Extensions.invokeScriptMethod<ProjectInfo> "__defaults__" parseContext

                match result with
                | Extensions.Success result -> result
                | Extensions.ScriptNotFound -> TerrabuildException.Raise($"Script {init} was not found")
                | Extensions.TargetNotFound -> ProjectInfo.Default // NOTE: if __defaults__ is not found - this will silently use default configuration, probably emit warning
                | Extensions.ErrorTarget exn -> TerrabuildException.Raise($"Invocation failure of command '__defaults__' for extension '{init}'", exn)
            | _ -> ProjectInfo.Default

        let projectInfo = {
            projectInfo
            with Ignores = projectInfo.Ignores + (projectConfig.Project.Ignores |> Option.defaultValue Set.empty)
                 Outputs = projectInfo.Outputs + (projectConfig.Project.Outputs |> Option.defaultValue Set.empty)
                 Dependencies = projectInfo.Dependencies + (projectConfig.Project.Dependencies |> Option.defaultValue Set.empty)
                 Includes = projectInfo.Includes + (projectConfig.Project.Includes |> Option.defaultValue Set.empty) }

        let labels = projectConfig.Project.Labels

        let projectOutputs = projectInfo.Outputs
        let projectIgnores = projectInfo.Ignores
        // convert relative dependencies to absolute dependencies respective to workspaceDirectory
        let projectDependencies =
            projectInfo.Dependencies
            |> Set.map (fun dep -> FS.workspaceRelative workspaceDir projectDir dep)

        let projectTargets = projectConfig.Targets

        let includes =
            projectScripts
            |> Seq.choose (fun (KeyValue(_, script)) -> script)
            |> Set.ofSeq
            |> Set.union projectInfo.Includes

        { LoadedProject.Dependencies = projectDependencies
          LoadedProject.Includes = includes
          LoadedProject.Ignores = projectIgnores
          LoadedProject.Outputs = projectOutputs
          LoadedProject.Targets = projectTargets
          LoadedProject.Labels = labels
          LoadedProject.Extensions = extensions
          LoadedProject.Scripts = scripts }


    // this is the final stage: create targets and create the project
    let finalizeProject projectId (projectDef: LoadedProject) (projectDependencies: Map<string, Project>) =
        let projectDir = projectId

        // get dependencies on files
        let files =
            projectDir
            |> IO.enumerateFilesBut (projectDef.Includes) (projectDef.Outputs + projectDef.Ignores)
            |> Set
        let filesHash =
            files
            |> Seq.sort
            |> Hash.sha256files

        let versions =
            projectDependencies
            |> Map.map (fun _ depProj -> depProj.Hash)

        let dependenciesHash =
            versions.Values
            |> Seq.sort
            |> Hash.sha256strings

        // NOTE: this is the hash (modulo target name) used for reconcialiation across executions
        let projectHash =
            [ filesHash; dependenciesHash ]
            |> Hash.sha256strings

        let projectSteps =
            projectDef.Targets
            |> Map.map (fun targetName target ->
                // use value from project target
                // otherwise use workspace target
                // defaults to allow caching
                let rebuild =
                    target.Rebuild
                    |> Option.defaultWith (fun () ->
                        workspaceConfig.Targets
                        |> Map.tryFind targetName
                        |> Option.map (fun target -> target.Rebuild)
                        |> Option.defaultValue false
                    )

                let usedVariables, actions =
                    target.Steps
                    |> List.fold (fun (usedVariables, actions) step ->
                        // let stepVars: Map<string, string> = Map.empty

                        let extension = 
                            match projectDef.Extensions |> Map.tryFind step.Extension with
                            | Some extension -> extension
                            | _ -> TerrabuildException.Raise($"Extension {step.Extension} is not defined")

                        let stepActions, stepVars =
                            let actionContext = { Terrabuild.Extensibility.ActionContext.Debug = options.Debug
                                                  Terrabuild.Extensibility.ActionContext.Directory = projectDir
                                                  Terrabuild.Extensibility.ActionContext.CI = sourceControl.CI
                                                  Terrabuild.Extensibility.ActionContext.NodeHash = projectHash
                                                  Terrabuild.Extensibility.ActionContext.Command = step.Command
                                                  Terrabuild.Extensibility.ActionContext.BranchOrTag = branchOrTag }

                            let actionVariables =
                                buildVariables
                                |> Map.add "terrabuild_project" (Expr.String projectId)
                                |> Map.add "terrabuild_target" (Expr.String targetName)
                                |> Map.add "terrabuild_configuration" (Expr.String configuration)
                                |> Map.add "terrabuild_branch_or_tag" (Expr.String branchOrTag)
                                |> (fun map ->
                                    let tagValue =
                                        match tag with
                                        | Some tag -> Expr.String tag
                                        | _ -> Expr.Nothing
                                    map |> Map.add "terrabuild_tag" tagValue)

                            let evaluationContext = {
                                Eval.EvaluationContext.WorkspaceDir = workspaceDir
                                Eval.EvaluationContext.ProjectDir = projectDir
                                Eval.EvaluationContext.Versions = versions
                                Eval.EvaluationContext.Variables = actionVariables
                            }

                            let usedVars, actionContext =
                                extension.Defaults
                                |> Map.addMap step.Parameters
                                |> Map.add "context" (Expr.Object actionContext)
                                |> Expr.Map
                                |> Eval.eval evaluationContext

                            // tag shall not trigger a build so we do not want dependency on this
                            let usedVars = usedVars |> Set.remove "terrabuild_tag"

                            let script = Extensions.getScript step.Extension projectDef.Scripts
                            let actionGroup =
                                let result =
                                    script
                                    |> Extensions.invokeScriptMethod<Terrabuild.Extensibility.ActionSequence> step.Command actionContext
                                match result with
                                | Extensions.Success result -> result
                                | Extensions.ScriptNotFound -> TerrabuildException.Raise($"Script {step.Extension} was not found")
                                | Extensions.TargetNotFound -> TerrabuildException.Raise($"Script {step.Extension} has no function {step.Command}")
                                | Extensions.ErrorTarget exn -> TerrabuildException.Raise($"Invocation failure of command '{step.Command}' for extension '{step.Extension}'", exn)

                            let batchContext =
                                if actionGroup.Batchable then
                                    Some { Script = script.Value
                                           Command = step.Command
                                           Context = actionContext }
                                else
                                    None

                            // rebuild semantic is implemented by tweaking cacheability
                            let cache = 
                                if rebuild then Cacheability.Never
                                else actionGroup.Cache

                            let containedActionBatch = {
                                ContaineredActionBatch.MetaCommand = $"{step.Extension} {step.Command}"
                                ContaineredActionBatch.BatchContext = batchContext
                                ContaineredActionBatch.Container = extension.Container
                                ContaineredActionBatch.ContainerVariables = extension.Variables
                                ContaineredActionBatch.Cache = cache
                                ContaineredActionBatch.Actions = actionGroup.Actions
                            }

                            containedActionBatch, usedVars

                        let usedVariables = usedVariables + stepVars
                        let actions = actions @ [ stepActions ]
                        usedVariables, actions
                    ) (Set.empty, [])

                let usedVariables =
                    usedVariables
                    |> Seq.sort
                    |> Seq.choose (fun k ->
                        match buildVariables |> Map.tryFind k with
                        | Some v -> Some (k, $"{v}")
                        | _ -> None)

                let variableHash =
                    usedVariables
                    |> Seq.map (fun (key, value) -> $"{key} = {value}")
                    |> Hash.sha256strings

                let stepHash =
                    actions
                    |> Seq.collect (fun batch ->
                        batch.Actions |> Seq.map(fun step ->
                            $"{batch.Container} {step.Command} {step.Arguments}"))
                    |> Hash.sha256strings

                let hash =
                    [ stepHash; variableHash ]
                    |> Hash.sha256strings

                // use value from project target
                // otherwise use workspace target
                // defaults to no dependencies
                let dependsOn =
                    target.DependsOn
                    |> Option.defaultWith (fun () ->
                        workspaceConfig.Targets
                        |> Map.tryFind targetName
                        |> Option.map (fun target -> target.DependsOn)
                        |> Option.defaultValue Set.empty
                    )

                let outputs =
                    match target.Outputs with
                    | Some outputs -> outputs
                    | _ -> projectDef.Outputs

                { ContaineredTarget.Hash = hash
                  ContaineredTarget.Variables = usedVariables |> Map.ofSeq
                  ContaineredTarget.Actions = actions
                  ContaineredTarget.DependsOn = dependsOn
                  ContaineredTarget.Outputs = outputs }
            )

        let files = files |> Set.map (FS.relativePath projectDir)

        { Project.Id = projectId
          Project.Hash = projectHash
          Project.Dependencies = projectDef.Dependencies
          Project.Files = files
          Project.Targets = projectSteps
          Project.Labels = projectDef.Labels }


    let projectFiles = 
        let rec findDependencies dir =
            seq {
                let projectFile =  FS.combinePath dir "PROJECT" 
                match projectFile with
                | FS.File file ->
                    file |> FS.parentDirectory |> FS.relativePath workspaceDir
                | _ ->
                    for subdir in dir |> IO.enumerateDirs do
                        yield! findDependencies subdir
            }

        findDependencies workspaceDir

    let projects = ConcurrentDictionary<string, Project>()
    let hub = Hub.Create(options.MaxConcurrency)
    for projectId in projectFiles do
        // parallel load of projects
        hub.Subscribe Array.empty (fun () ->
            // load project
            let loadedProject = loadProjectDef projectId

            // await dependencies to be loaded
            let awaitedProjects =
                loadedProject.Dependencies
                |> Seq.map (fun awaitedProjectId -> hub.GetComputed<Project> awaitedProjectId)
                |> Array.ofSeq

            let awaitedSignals = awaitedProjects |> Array.map (fun entry -> entry :> ISignal)
            hub.Subscribe awaitedSignals (fun () ->
                // build task & code & notify
                let projectDependencies = 
                    awaitedProjects
                    |> Seq.map (fun projectDependency -> projectDependency.Name, projectDependency.Value)
                    |> Map.ofSeq
    
                let project = finalizeProject projectId loadedProject projectDependencies
                projects.TryAdd(projectId, project) |> ignore

                let loadedProjectSignal = hub.CreateComputed<Project> projectId
                loadedProjectSignal.Value <- project)
        )

    let status = hub.WaitCompletion()
    match status with
    | Status.Ok -> ()
    | Status.SubcriptionNotRaised projectId -> TerrabuildException.Raise($"Project {projectId} is unknown")
    | Status.SubscriptionError exn -> TerrabuildException.Raise("Failed to load configuration", exn)

    // select dependencies with labels if any
    let dependencies =
        match labels with
        | Some labels ->
            projects
            |> Seq.choose (fun (KeyValue(dependency, config)) ->
                if Set.intersect config.Labels labels <> Set.empty then Some dependency else None)
        | _ -> projects.Keys
        |> Set

    { Workspace.Space = workspaceConfig.Space
      Workspace.ComputedProjectSelection = dependencies
      Workspace.Projects = projects |> Map.ofDict
      Workspace.Targets = workspaceConfig.Targets
      Workspace.Configuration = configuration
      Workspace.Note = note
      Workspace.Tag = tag
      Workspace.SourceControl = sourceControl }
