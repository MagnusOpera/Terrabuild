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
    Workspace: string
    WhatIf: bool
    Debug: bool
    MaxConcurrency: int
    Force: bool
    Retry: bool
    LocalOnly: bool
    StartedAt: DateTime
    NoContainer: bool
    NoBatch: bool
    Targets: string set
    CI: string option
    BranchOrTag: string
    Configuration: string
    Note: string option
    Tag: string option
    Labels: string set option
    Variables: Map<string, string>
}

[<RequireQualifiedAccess>]
type TargetOperation = {
    Hash: string
    Container: string option
    ContainerVariables: string set
    Extension: string
    Command: string
    Script: Terrabuild.Scripting.Script
    Context: Value
}
with
    static member MarkAsForced = {
        Hash = "--"
        Container = None
        ContainerVariables = Set.empty
        Extension = ""
        Command = "👀"
        Script = Terrabuild.Scripting.Script(typeof<Unit>)
        Context = Value.Nothing
    }

[<RequireQualifiedAccess>]
type Target = {
    Hash: string
    Rebuild: bool
    DependsOn: string set
    Outputs: string set
    Operations: TargetOperation list
}


[<RequireQualifiedAccess>]
type Project = {
    Id: string
    Hash: string
    Dependencies: string set
    Files: string set
    Targets: Map<string, Target>
    Labels: string set
}

[<RequireQualifiedAccess>]
type Workspace = {
    // Space to use
    Space: string option

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
    Includes: string set
    Ignores: string set
    Outputs: string set
    Targets: Map<string, Terrabuild.Configuration.Project.AST.Target>
    Labels: string set
    Extensions: Map<string, Extension>
    Scripts: Map<string, LazyScript>
}


let read (options: Options) =
    $"{Ansi.Emojis.box} Reading {options.Configuration} configuration" |> Terminal.writeLine

    if options.Force then
        $" {Ansi.Styles.yellow}{Ansi.Emojis.bang}{Ansi.Styles.reset} force build requested" |> Terminal.writeLine

    if options.Retry then
        $" {Ansi.Styles.yellow}{Ansi.Emojis.bang}{Ansi.Styles.reset} retry build requested" |> Terminal.writeLine

    options.CI
    |> Option.iter (fun ci -> $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} source control is {ci}" |> Terminal.writeLine)

    let workspaceContent = FS.combinePath options.Workspace "WORKSPACE" |> File.ReadAllText
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

    // variables = default configuration vars + configuration vars + env vars + args vars
    let defaultVariables =
        match workspaceConfig.Configurations |> Map.tryFind "default" with
        | Some config -> config.Variables
        | _ -> Map.empty

    let configVariables =
        match workspaceConfig.Configurations |> Map.tryFind options.Configuration with
        | Some variables -> variables.Variables
        | _ ->
            match options.Configuration with
            | "default" -> Map.empty
            | _ -> TerrabuildException.Raise($"Configuration '{options.Configuration}' not found")

    let buildVariables =
        defaultVariables
        |> Map.addMap configVariables
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

    let branchOrTag = options.BranchOrTag

    let extensions = 
        Extensions.systemExtensions
        |> Map.addMap workspaceConfig.Extensions

    let scripts =
        extensions
        |> Map.map (fun _ _ -> None)
        |> Map.map Extensions.lazyLoadScript


    // this is the first stage: load project and mostly get dependencies references
    let loadProjectDef projectId =
        let projectDir = FS.combinePath options.Workspace projectId
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
                                    Terrabuild.Extensibility.ExtensionContext.CI = options.CI.IsSome }
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
            |> Set.map (fun dep -> FS.workspaceRelative options.Workspace projectDir dep)

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
            [ projectId; filesHash; dependenciesHash ]
            |> Hash.sha256strings

        let projectSteps =
            projectDef.Targets
            |> Map.map (fun targetName target ->

                let actionVariables =
                    buildVariables
                    |> Map.add "terrabuild_project" (Expr.String projectId)
                    |> Map.add "terrabuild_target" (Expr.String targetName)
                    |> Map.add "terrabuild_hash" (Expr.String projectHash)
                    |> Map.add "terrabuild_configuration" (Expr.String options.Configuration)
                    |> Map.add "terrabuild_branch_or_tag" (Expr.String branchOrTag)
                    |> Map.add "terrabuild_retry" (Expr.Boolean options.Retry)
                    |> Map.add "terrabuild_force" (Expr.Boolean options.Force)
                    |> (fun map ->
                        let tagValue =
                            match options.Tag with
                            | Some tag -> Expr.String tag
                            | _ -> Expr.Nothing
                        map |> Map.add "terrabuild_tag" tagValue)
                    |> (fun map ->
                        let noteValue =
                            match options.Note with
                            | Some note -> Expr.String note
                            | _ -> Expr.Nothing
                        map |> Map.add "terrabuild_note" noteValue)

                let evaluationContext = {
                    Eval.EvaluationContext.WorkspaceDir = options.Workspace
                    Eval.EvaluationContext.ProjectDir = projectDir
                    Eval.EvaluationContext.Versions = versions
                    Eval.EvaluationContext.Variables = actionVariables
                }

                // use value from project target
                // otherwise use workspace target
                // defaults to allow caching
                let _, rebuild =
                    let rebuild =
                        target.Rebuild
                        |> Option.defaultWith (fun () ->
                            workspaceConfig.Targets
                            |> Map.tryFind targetName
                            |> Option.map (fun target -> target.Rebuild)
                            |> Option.defaultValue (Expr.Boolean false))
                    Eval.eval evaluationContext rebuild
                let rebuild =
                    match rebuild with
                    | Value.Bool rebuild -> rebuild
                    | _ -> TerrabuildException.Raise("rebuild must evaluate to a bool")

                let targetOperations =
                    target.Steps
                    |> List.fold (fun actions step ->
                        let extension = 
                            match projectDef.Extensions |> Map.tryFind step.Extension with
                            | Some extension -> extension
                            | _ -> TerrabuildException.Raise($"Extension {step.Extension} is not defined")

                        let usedVars, context =
                            extension.Defaults
                            |> Map.addMap step.Parameters
                            |> Expr.Map
                            |> Eval.eval evaluationContext

                        let script =
                            match Extensions.getScript step.Extension projectDef.Scripts with
                            | Some script -> script
                            | _ -> TerrabuildException.Raise($"Extension {step.Extension} is not defined")

                        let usedVariables =
                            usedVars
                            |> Seq.sort
                            |> Seq.choose (fun key ->
                                match buildVariables |> Map.tryFind key with
                                | Some value -> Some $"{key} = {value}"
                                | _ -> None)
                            |> List.ofSeq

                        let hash =
                            [ step.Extension; step.Command ] @ usedVariables
                            |> Hash.sha256strings

                        let targetContext = {
                            TargetOperation.Hash = hash
                            TargetOperation.Container = extension.Container
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
                        |> Option.defaultValue Set.empty
                    )

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
                    Target.Outputs = outputs
                    Target.Operations = targetOperations
                }

                target
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
                    file |> FS.parentDirectory |> FS.relativePath options.Workspace
                | _ ->
                    for subdir in dir |> IO.enumerateDirs do
                        yield! findDependencies subdir
            }

        findDependencies options.Workspace

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
    let selectedProjects =
        match options.Labels with
        | Some labels ->
            projects
            |> Seq.choose (fun (KeyValue(dependency, config)) ->
                if Set.intersect config.Labels labels <> Set.empty then Some dependency else None)
        | _ -> projects.Keys
        |> Set

    { Workspace.Space = workspaceConfig.Space
      Workspace.SelectedProjects = selectedProjects
      Workspace.Projects = projects |> Map.ofDict
      Workspace.Targets = workspaceConfig.Targets }
