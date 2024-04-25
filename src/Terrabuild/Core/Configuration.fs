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

[<RequireQualifiedAccess>]
type Options = {
    WhatIf: bool
    Debug: bool
    MaxConcurrency: int
    Force: bool
    Retry: bool
    StartedAt: DateTime
}

type BatchContext = {
    Script: Terrabuild.Scripting.Script
    Command: string
    Context: Value
}

[<RequireQualifiedAccess>]
type ContaineredActionBatch = {
    Cache: Cacheability
    Actions: Action list

    Container: string option
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
    Space: string option
    SourceControl: Contracts.SourceControl
    Dependencies: string set
    Targets: Map<string, Terrabuild.Configuration.Workspace.AST.Target>
    Projects: Map<string, Project>
    Environment: string
}


let read workspaceDir environment labels variables (sourceControl: Contracts.SourceControl) (options: Options) =
    $"{Ansi.Emojis.box} Reading configuration using environment {environment}" |> Terminal.writeLine

    let workspaceContent = FS.combinePath workspaceDir "WORKSPACE" |> File.ReadAllText
    let workspaceConfig =
        try
            Terrabuild.Configuration.FrontEnd.parseWorkspace workspaceContent
        with exn ->
            TerrabuildException.Raise("Failed to read WORKSPACE configuration file", exn)

    // create temporary folder so extensions can expose files to docker containers (folder must within workspaceDir hierarchy)
    if options.WhatIf |> not then FS.combinePath workspaceDir ".terrabuild" |> IO.createDirectory

    // variables
    let environments = workspaceConfig.Environments
    let envVariables =
        match environments |> Map.tryFind environment with
        | Some variables -> variables.Variables
        | _ -> TerrabuildException.Raise($"Environment '{environment}' not found")
    let buildVariables =
        variables
        |> Map.map (fun _ value -> value)
        |> Map.addMap envVariables

    if options.Force then
        $" {Ansi.Styles.yellow}{Ansi.Emojis.bang}{Ansi.Styles.reset} force build requested" |> Terminal.writeLine

    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} source control is {sourceControl.Name}" |> Terminal.writeLine

    let branchOrTag = sourceControl.BranchOrTag

    let processedNodes = ConcurrentDictionary<string, bool>()

    let extensions = 
        Extensions.systemExtensions
        |> Map.addMap workspaceConfig.Extensions

    let scripts =
        extensions
        |> Map.map (fun _ _ -> None)
        |> Map.map Extensions.lazyLoadScript

    let rec scanDependency projects project =
        let projectDir = project

        // process only unknown dependency
        if processedNodes.TryAdd(project, true) then
            let projectFile = FS.combinePath projectDir "PROJECT"
            let projectContent = File.ReadAllText projectFile
            let projectConfig =
                try
                    Terrabuild.Configuration.FrontEnd.parseProject projectContent
                with exn ->
                    TerrabuildException.Raise($"Failed to read PROJECT configuration {projectFile}", exn)

            let projectDef =
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
                    match projectConfig.Configuration.Init with
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
                        | Extensions.ScriptNotFound -> TerrabuildException.Raise $"Script {init} was not found"
                        | Extensions.TargetNotFound -> ProjectInfo.Default // NOTE: if __defaults__ is not found - this will silently use default configuration, probably emit warning
                        | Extensions.ErrorTarget exn -> TerrabuildException.Raise $"Invocation failure of __defaults__ of script {init}" exn
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
                    |> Set.map (fun dep -> FS.workspaceRelative workspaceDir projectDir dep)

                let projectTargets = projectConfig.Targets

                let includes =
                    projectScripts
                    |> Seq.choose (fun (KeyValue(_, script)) -> script)
                    |> Set.ofSeq

                {| Dependencies = projectDependencies
                   Includes = includes
                   Ignores = projectIgnores
                   Outputs = projectOutputs
                   Targets = projectTargets
                   Labels = labels
                   Extensions = extensions
                   Scripts = scripts |}

            // we go depth-first in order to compute node hash right after
            // NOTE: this could lead to a memory usage problem
            let projects: Map<string, Project> =
                try
                    scanDependencies projects projectDef.Dependencies
                with
                    ex ->
                        TerrabuildException.Raise($"while processing project '{project}'", ex)


            // check for circular or missing dependencies
            for childDependency in projectDef.Dependencies do
                if projects |> Map.tryFind childDependency |> Option.isNone then
                    TerrabuildException.Raise($"Circular dependencies between {project} and {childDependency}")


            // get dependencies on files
            let files =
                projectDir |> IO.enumerateFilesBut (projectDef.Outputs + projectDef.Ignores)
                |> Set
                |> Set.union projectDef.Includes
            let filesHash =
                files
                |> Seq.sort
                |> Hash.sha256files
 
            let versions =
                projectDef.Dependencies
                |> Seq.map (fun dependency -> 
                    match projects |> Map.tryFind dependency with
                    | Some project -> dependency, project.Hash
                    | _ -> TerrabuildException.Raise($"Circular dependencies between '{project}' and '{dependency}'"))
                |> Map.ofSeq

            let dependenciesHash =
                versions.Values
                |> Seq.sort
                |> Hash.sha256strings

            // NOTE: this is the hash (modulo target name) used for reconcialiation across executions
            let projectHash =
                [ project; filesHash; dependenciesHash ]
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
                            |> Option.bind (fun target -> target.Rebuild)
                            |> Option.defaultValue false
                        )

                    let usedVariables, actions =
                        target.Steps
                        |> List.fold (fun (usedVariables, actions) step ->
                            // let stepVars: Map<string, string> = Map.empty

                            let extension = 
                                match projectDef.Extensions |> Map.tryFind step.Extension with
                                | Some extension -> extension
                                | _ -> TerrabuildException.Raise $"Extension {step.Extension} is not defined"

                            let stepActions, stepVars =
                                let actionContext = { Terrabuild.Extensibility.ActionContext.Debug = options.Debug
                                                      Terrabuild.Extensibility.ActionContext.Directory = projectDir
                                                      Terrabuild.Extensibility.ActionContext.CI = sourceControl.CI
                                                      Terrabuild.Extensibility.ActionContext.NodeHash = projectHash
                                                      Terrabuild.Extensibility.ActionContext.Command = step.Command
                                                      Terrabuild.Extensibility.ActionContext.BranchOrTag = branchOrTag }

                                let actionVariables =
                                    buildVariables
                                    |> Map.add "terrabuild_project" (Expr.String project)
                                    |> Map.add "terrabuild_target" (Expr.String targetName)

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

                                let script = Extensions.getScript step.Extension projectDef.Scripts
                                let actionGroup =
                                    let result =
                                        script
                                        |> Extensions.invokeScriptMethod<Terrabuild.Extensibility.ActionSequence> step.Command actionContext
                                    match result with
                                    | Extensions.Success result -> result
                                    | Extensions.ScriptNotFound -> TerrabuildException.Raise $"Script {step.Extension} was not found"
                                    | Extensions.TargetNotFound -> TerrabuildException.Raise $"Script {step.Extension} has no function {step.Command}"
                                    | Extensions.ErrorTarget exn -> TerrabuildException.Raise $"Invocation failure of {step.Command} of script {step.Extension}" exn

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
                                    ContaineredActionBatch.BatchContext = batchContext
                                    ContaineredActionBatch.Container = extension.Container
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
                        |> Seq.map (fun k -> k, $"{buildVariables[k]}")

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
                            |> Option.bind (fun target -> target.DependsOn)
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

            let files =
                files
                |> Set.map (FS.relativePath projectDir)

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
            let projectFile =  FS.combinePath dir "PROJECT" 
            match projectFile with
            | FS.File file ->
                file |> FS.parentDirectory |> FS.relativePath workspaceDir
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

    { Workspace.Space = workspaceConfig.Space
      Workspace.Dependencies = dependencies
      Workspace.Projects = projects
      Workspace.Targets = workspaceConfig.Targets
      Workspace.Environment = environment
      Workspace.SourceControl = sourceControl }
