module Configuration
open System.IO
open Collections
open System
open System.Collections.Concurrent
open Terrabuild.Extensibility
open Terrabuild.Expressions
open Terrabuild.Configuration.Project.AST
open Terrabuild.Configuration.AST
open Errors
open Terrabuild.PubSub
open Microsoft.Extensions.FileSystemGlobbing
open Serilog

[<RequireQualifiedAccess>]
type TargetOperation = {
    Hash: string
    Container: string option
    Platform: string option
    ContainerVariables: string set
    Extension: string
    Command: string
    Script: Terrabuild.Scripting.Script
    Context: Value
}

[<RequireQualifiedAccess>]
type Target = {
    Hash: string
    Rebuild: bool
    DependsOn: string set
    Outputs: string set
    Cache: Cacheability option
    Operations: TargetOperation list
}


[<RequireQualifiedAccess>]
type Project = {
    Name: string
    Hash: string
    Dependencies: string set
    Files: string set
    Targets: Map<string, Target>
    Labels: string set
}

[<RequireQualifiedAccess>]
type Workspace = {
    // Space to use
    Id: string option

    // Computed projects selection (derived from user inputs)
    SelectedProjects: string set

    // All targets at workspace level
    Targets: Map<string, Terrabuild.Configuration.Workspace.AST.Target>

    // All discovered projects in workspace
    Projects: Map<string, Project>
}

type private LazyScript = Lazy<Terrabuild.Scripting.Script>

[<RequireQualifiedAccess>]
type private LoadedProject = {
    Dependencies: string set
    Links: string set
    Includes: string set
    Ignores: string set
    Outputs: string set
    Targets: Map<string, Terrabuild.Configuration.Project.AST.Target>
    Labels: string set
    Extensions: Map<string, Extension>
    Scripts: Map<string, LazyScript>
}


let scanFolders root (ignores: Set<string>) =
    let matcher = Matcher()
    matcher.AddInclude("**/*").AddExcludePatterns(ignores)

    fun dir ->
        // exclude sub-folders with WORKSPACE
        let relativeDir = dir |> FS.relativePath root
        if matcher.Match(relativeDir).HasMatches then
            match FS.combinePath dir "WORKSPACE" with
            | FS.File _ -> false
            | _ -> true
        else
            false


let read (options: ConfigOptions.Options) =
    $"{Ansi.Emojis.box} Reading {options.Configuration} configuration" |> Terminal.writeLine

    if options.Force then
        $" {Ansi.Styles.yellow}{Ansi.Emojis.bang}{Ansi.Styles.reset} force build requested" |> Terminal.writeLine
    else
        if options.Retry then
            $" {Ansi.Styles.yellow}{Ansi.Emojis.bang}{Ansi.Styles.reset} retry build requested" |> Terminal.writeLine

    if options.WhatIf then
        $" {Ansi.Styles.yellow}{Ansi.Emojis.bang}{Ansi.Styles.reset} whatif mode requested" |> Terminal.writeLine

    options.Run
    |> Option.iter (fun run -> $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} source control is {run.Name}" |> Terminal.writeLine)

    let workspaceContent = FS.combinePath options.Workspace "WORKSPACE" |> File.ReadAllText
    let workspaceConfig =
        try
            Terrabuild.Configuration.Workspace.FrontEnd.parseWorkspace workspaceContent
        with exn ->
            raiseParseError "Failed to read WORKSPACE configuration file" exn

    let evaluationContext =
        let convertToVarType (key: string) (existingValue: Value) (value: string) =
            match existingValue with
            | Value.String _ ->
                Value.String value
            | Value.Number _ ->
                match value |> Int32.TryParse with
                | true, value -> Value.Number value
                | _ -> raiseTypeError $"Value '{value}' can't be converted to number variable {key}"
            | Value.Bool _ ->
                match value |> Boolean.TryParse with
                | true, value -> Value.Bool value
                | _ -> raiseTypeError $"Value '{value}' can't be converted to boolean variable {key}"
            | _ -> raiseTypeError $"Value 'value' can't be converted to variable {key}"

        let tagValue = 
            match options.Tag with
            | Some tag -> Value.String tag
            | _ -> Value.Nothing

        let noteValue =
            match options.Note with
            | Some note -> Value.String note
            | _ -> Value.Nothing

        let defaultEvaluationContext = {
            Eval.EvaluationContext.WorkspaceDir = options.Workspace
            Eval.EvaluationContext.ProjectDir = None
            Eval.EvaluationContext.Versions = Map.empty
            Eval.EvaluationContext.Variables = Map [
                "terrabuild_configuration", Value.String options.Configuration
                "terrabuild_branch_or_tag", Value.String options.BranchOrTag 
                "terrabuild_head_commit", Value.String options.HeadCommit.Sha
                "terrabuild_retry", Value.Bool options.Retry 
                "terrabuild_force", Value.Bool options.Force 
                "terrabuild_ci", Value.Bool options.Run.IsSome 
                "terrabuild_debug", Value.Bool options.Debug 
                "terrabuild_tag", tagValue 
                "terrabuild_note", noteValue ]
        }

        // variables = default configuration vars + configuration vars + env vars + args vars
        let evaluationContext =
            let defaultVariables =
                match workspaceConfig.Configurations |> Map.tryFind "default" with
                | Some config ->
                    config.Variables
                    |> Map.map (fun _ expr -> Eval.eval defaultEvaluationContext expr)
                | _ -> Map.empty

            { defaultEvaluationContext with Eval.Variables = defaultEvaluationContext.Variables |> Map.addMap defaultVariables }

        let evaluationContext =
            let configVariables =
                match workspaceConfig.Configurations |> Map.tryFind options.Configuration with
                | Some variables ->
                    variables.Variables
                    |> Map.map (fun _ expr -> Eval.eval evaluationContext expr)
                | _ ->
                    match options.Configuration with
                    | "default" -> Map.empty
                    | _ -> raiseSymbolError $"Configuration '{options.Configuration}' not found"

            { evaluationContext with Eval.Variables = evaluationContext.Variables |> Map.addMap configVariables }
        
        let evaluationContext =
            let buildVariables =
                evaluationContext.Variables
                // override variable with configuration variable if any
                |> Map.map (fun key expr ->
                    match $"TB_VAR_{key |> String.toLower}" |> Environment.GetEnvironmentVariable with
                    | null -> expr
                    | value -> convertToVarType key expr value)
                // override variable with provided ones on command line if any
                |> Map.map (fun key expr ->
                    match options.Variables |> Map.tryFind (key |> String.toLower) with
                    | Some value -> convertToVarType key expr value
                    | _ -> expr)

            { evaluationContext with Eval.Variables = buildVariables }

        evaluationContext


    let extensions, scripts =
        // load system extensions
        let sysScripts =
            Extensions.systemExtensions
            |> Map.map (fun _ _ -> None)
            |> Map.map Extensions.lazyLoadScript

        // load user extension
        let usrScripts =
            workspaceConfig.Extensions
            |> Map.map (fun _ ext ->
                match ext.Script with
                | Some script -> script |> FS.workspaceRelative options.Workspace "" |> Some
                | _ -> None)
            |> Map.map Extensions.lazyLoadScript

        let extensions =
            Extensions.systemExtensions
            |> Map.addMap workspaceConfig.Extensions

        let scripts = sysScripts |> Map.addMap usrScripts

        extensions, scripts


    // this is the first stage: load project and mostly get dependencies references
    let loadProjectDef projectId =
        let projectDir = FS.combinePath options.Workspace projectId
        let projectFile = FS.combinePath projectDir "PROJECT"
        let slashedProjectId = $"{projectId}/"

        Log.Debug("Loading project definition {ProjectId}", projectId)

        let projectConfig =
            match projectFile with
            | FS.File projectFile ->
                let projectContent = File.ReadAllText projectFile
                try Terrabuild.Configuration.Project.FrontEnd.parseProject projectContent
                with exn -> forwardExternalError $"Failed to read PROJECT configuration '{projectId}'" exn
            | _ ->
                raiseInvalidArg $"No PROJECT found in directory '{projectFile}'"

        let extensions = extensions |> Map.addMap projectConfig.Extensions

        let projectScripts =
            projectConfig.Extensions
            |> Map.map (fun _ ext -> ext.Script |> Option.map (FS.workspaceRelative options.Workspace projectDir))

        let scripts =
            scripts
            |> Map.addMap (projectScripts |> Map.map Extensions.lazyLoadScript)

        let projectInfo =
            match projectConfig.Project.Init with
            | Some init ->
                let parseContext = 
                    let context = { Terrabuild.Extensibility.ExtensionContext.Debug = options.Debug
                                    Terrabuild.Extensibility.ExtensionContext.Directory = projectDir
                                    Terrabuild.Extensibility.ExtensionContext.CI = options.Run.IsSome }
                    Value.Map (Map [ "context", Value.Object context ])

                let result =
                    Extensions.getScript init scripts
                    |> Extensions.invokeScriptMethod<ProjectInfo> "__defaults__" parseContext

                match result with
                | Extensions.Success result -> result
                | Extensions.ScriptNotFound -> raiseSymbolError $"Script {init} was not found"
                | Extensions.TargetNotFound -> ProjectInfo.Default // NOTE: if __defaults__ is not found - this will silently use default configuration, probably emit warning
                | Extensions.ErrorTarget exn -> forwardExternalError $"Invocation failure of command '__defaults__' for extension '{init}'" exn
            | _ -> ProjectInfo.Default

        let projectInfo = {
            projectInfo
            with Ignores = projectInfo.Ignores + projectConfig.Project.Ignores
                 Outputs = projectInfo.Outputs + projectConfig.Project.Outputs
                 Dependencies = projectInfo.Dependencies + projectConfig.Project.Dependencies
                 Links = projectInfo.Links + projectConfig.Project.Links
                 Includes = projectInfo.Includes + projectConfig.Project.Includes }

        let labels = projectConfig.Project.Labels

        let projectOutputs = projectInfo.Outputs
        let projectIgnores = projectInfo.Ignores
        // convert relative dependencies to absolute dependencies respective to workspaceDirectory
        let projectDependencies =
            projectInfo.Dependencies
            |> Set.map (fun dep -> FS.workspaceRelative options.Workspace projectDir dep)
        let projectLinks =
            projectInfo.Links
            |> Set.map (fun dep -> FS.workspaceRelative options.Workspace projectDir dep)
            |> Set.filter (fun dep -> dep |> String.startsWith slashedProjectId |> not)

        let projectTargets = projectConfig.Targets

        let includes =
            projectScripts
            |> Seq.choose (fun (KeyValue(_, script)) -> script)
            |> Set.ofSeq
            |> Set.union projectInfo.Includes

        { LoadedProject.Dependencies = projectDependencies
          LoadedProject.Links = projectLinks
          LoadedProject.Includes = includes
          LoadedProject.Ignores = projectIgnores
          LoadedProject.Outputs = projectOutputs
          LoadedProject.Targets = projectTargets
          LoadedProject.Labels = labels
          LoadedProject.Extensions = extensions
          LoadedProject.Scripts = scripts }


    // this is the final stage: create targets and create the project
    let finalizeProject projectDir (projectDef: LoadedProject) (projectReferences: Map<string, Project>) =
        let projectId = projectDir |> String.toUpper
        let tbFiles = Set [ "WORKSPACE"; "PROJECT" ]

        // get dependencies on files
        let files =
            projectDir
            |> IO.enumerateFilesBut projectDef.Includes (projectDef.Outputs + projectDef.Ignores + tbFiles)
            |> Set

        let filesHash =
            files
            |> Seq.sort
            |> Hash.sha256files

        let dependenciesHash =
            let versionDependencies =
                projectReferences
                |> Map.filter (fun projectId _ -> (Set.contains projectId projectDef.Dependencies) || (Set.contains projectId projectDef.Links))
                |> Map.map (fun _ depProj -> depProj.Hash)

            versionDependencies.Values
            |> Seq.sort
            |> Hash.sha256strings

        let versions = 
            projectReferences
            |> Map.map (fun _ depProj -> depProj.Hash)

        // NOTE: this is the hash (modulo target name) used for reconcialiation across executions
        let projectHash =
            [ projectId; filesHash; dependenciesHash ]
            |> Hash.sha256strings

        let projectSteps =
            projectDef.Targets
            |> Map.map (fun targetName target ->

                let evaluationContext =
                    let actionVariables =
                        Map [ "terrabuild_project", Value.String projectId
                              "terrabuild_target" , Value.String targetName
                              "terrabuild_hash", Value.String projectHash ]

                    { evaluationContext with
                        Eval.ProjectDir = Some projectDir
                        Eval.Versions = versions
                        Eval.Variables = evaluationContext.Variables |> Map.addMap actionVariables }

                // use value from project target
                // otherwise use workspace target
                // defaults to allow caching
                let rebuild =
                    let rebuild =
                        target.Rebuild
                        |> Option.defaultWith (fun () ->
                            workspaceConfig.Targets
                            |> Map.tryFind targetName
                            |> Option.map (fun target -> target.Rebuild)
                            |> Option.defaultValue (Expr.Bool false))
                    Eval.eval evaluationContext rebuild
                let rebuild =
                    match rebuild with
                    | Value.Bool rebuild -> rebuild
                    | _ -> raiseTypeError "rebuild must evaluate to a bool"

                let targetOperations =
                    target.Steps
                    |> List.fold (fun actions step ->
                        let extension = 
                            match projectDef.Extensions |> Map.tryFind step.Extension with
                            | Some extension -> extension
                            | _ -> raiseSymbolError $"Extension {step.Extension} is not defined"

                        let context =
                            extension.Defaults
                            |> Map.addMap step.Parameters
                            |> Expr.Map
                            |> Eval.eval evaluationContext

                        let container =
                            match extension.Container with
                            | Some container ->
                                match Eval.eval evaluationContext container with
                                | Value.String container -> Some container
                                | Value.Nothing -> None
                                | _ -> raiseTypeError "container must evaluate to a string"
                            | _ -> None

                        let platform =
                            match extension.Platform with
                            | Some platform ->
                                match Eval.eval evaluationContext platform with
                                | Value.String platform -> Some platform
                                | Value.Nothing -> None
                                | _ -> raiseTypeError "container must evaluate to a string"
                            | _ -> None

                        let script =
                            match Extensions.getScript step.Extension projectDef.Scripts with
                            | Some script -> script
                            | _ -> raiseSymbolError $"Extension {step.Extension} is not defined"

                        let hash =
                            let containerInfos = 
                                match container with
                                | Some container -> [ container ] @ List.ofSeq extension.Variables
                                | _ -> []

                            let platformInfos = 
                                match platform with
                                | Some platform -> [ platform ] @ List.ofSeq extension.Variables
                                | _ -> []

                            [ step.Extension; step.Command ] @ containerInfos @ platformInfos
                            |> Hash.sha256strings

                        let targetContext = {
                            TargetOperation.Hash = hash
                            TargetOperation.Container = container
                            TargetOperation.Platform = platform
                            TargetOperation.ContainerVariables = extension.Variables
                            TargetOperation.Extension = step.Extension
                            TargetOperation.Command = step.Command
                            TargetOperation.Script = script
                            TargetOperation.Context = context
                        }

                        let actions = actions @ [ targetContext ]
                        actions
                    ) []

                // use value from project target
                // otherwise use workspace target
                // defaults to no dependencies
                let dependsOn =
                    target.DependsOn
                    |> Option.defaultWith (fun () ->
                        workspaceConfig.Targets
                        |> Map.tryFind targetName
                        |> Option.map (fun target -> target.DependsOn)
                        |> Option.defaultValue Set.empty)

                let outputs =
                    match target.Outputs with
                    | Some outputs -> outputs
                    | _ -> projectDef.Outputs

                let hash =
                    targetOperations
                    |> List.map (fun ope -> ope.Hash)
                    |> Hash.sha256strings

                let target = {
                    Target.Hash = hash
                    Target.Rebuild = rebuild
                    Target.DependsOn = dependsOn
                    Target.Cache = target.Cache
                    Target.Outputs = outputs
                    Target.Operations = targetOperations
                }

                target
            )

        let files = files |> Set.map (FS.relativePath projectDir)

        let projectDependencies = projectDef.Dependencies |> Set.map String.toUpper

        { Project.Name = projectDir
          Project.Hash = projectHash
          Project.Dependencies = projectDependencies
          Project.Files = files
          Project.Targets = projectSteps
          Project.Labels = projectDef.Labels }



    let searchProjectsAndApply() =
        let scanFolder = scanFolders options.Workspace workspaceConfig.Workspace.Ignores
        let projectLoading = ConcurrentDictionary<string, bool>()
        let projects = ConcurrentDictionary<string, Project>()
        let hub = Hub.Create(options.MaxConcurrency)

        let rec loadProject projectDir =
            let projectId = projectDir |> String.toUpper
            if projectLoading.ContainsKey projectId |> not then
                projectLoading.TryAdd(projectId, true) |> ignore

                // load project and force loading all dependencies as well
                let loadedProject = loadProjectDef projectDir
                for dependency in loadedProject.Dependencies do
                    loadProject dependency

                // parallel load of projects
                hub.Subscribe projectDir Array.empty (fun () ->
                    // await dependencies to be loaded
                    let awaitedProjects =
                        (loadedProject.Dependencies + loadedProject.Links)
                        |> Set.map String.toUpper
                        |> Seq.map (fun awaitedProjectId -> hub.GetSignal<Project> awaitedProjectId)
                        |> Array.ofSeq

                    let awaitedSignals = awaitedProjects |> Array.map (fun entry -> entry :> ISignal)
                    hub.Subscribe projectDir awaitedSignals (fun () ->
                        // build task & code & notify
                        let projectDependencies = 
                            awaitedProjects
                            |> Seq.map (fun projectDependency -> projectDependency.Name, projectDependency.Value)
                            |> Map.ofSeq

                        let project = finalizeProject projectDir loadedProject projectDependencies
                        projects.TryAdd(projectId, project) |> ignore

                        let loadedProjectSignal = hub.GetSignal<Project> projectId
                        loadedProjectSignal.Value <- project)
                )

        let rec findDependencies isRoot dir =
            if isRoot || scanFolder  dir then
                let projectFile = FS.combinePath dir "PROJECT" 
                match projectFile with
                | FS.File file ->
                    let projectFile = file |> FS.parentDirectory |> FS.relativePath options.Workspace
                    loadProject projectFile
                | _ ->
                    for subdir in dir |> IO.enumerateDirs do
                        findDependencies false subdir

        findDependencies true options.Workspace
        let status = hub.WaitCompletion()
        match status with
        | Status.Ok ->
            projects |> Map.ofDict
        | Status.UnfulfilledSubscription (subscription, signals) ->
            let unraisedSignals = signals |> String.join ","
            raiseInvalidArg $"Project '{subscription}' has pending operations on '{unraisedSignals}'. Check for circular dependencies."
        | Status.SubscriptionError exn ->
            forwardExternalError "Failed to load configuration" exn


    let projects = searchProjectsAndApply()

    // select dependencies with labels if any
    let selectedProjects =
        match options.Labels with
        | Some labels ->
            projects
            |> Seq.choose (fun (KeyValue(dependency, config)) ->
                if Set.intersect config.Labels labels <> Set.empty then Some dependency else None)
        | _ -> projects.Keys
        |> Set

    { Workspace.Id = workspaceConfig.Workspace.Id
      Workspace.SelectedProjects = selectedProjects
      Workspace.Projects = projects |> Map.ofDict
      Workspace.Targets = workspaceConfig.Targets }
