module Configuration
open System.IO
open Collections
open System
open System.Collections.Concurrent
open Terrabuild.Extensibility
open Terrabuild.Expressions
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
    Targets: Map<string, Set<string>>

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
    Targets: Map<string, AST.Project.TargetBlock>
    Labels: string set
    Extensions: Map<string, AST.Common.ExtensionBlock>
    Scripts: Map<string, LazyScript>
    Locals: Map<string, Expr>
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


let (|Bool|Number|String|) (value: string) = 
    match value |> Boolean.TryParse with
    | true, value -> Bool value
    | _ ->
        match value |> Int32.TryParse with
        | true, value -> Number value
        | _ -> String value


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
            FrontEnd.Workspace.parse workspaceContent
        with exn ->
            raiseParserError("Failed to read WORKSPACE configuration file", exn)




    let tagValue = 
        match options.Tag with
        | Some tag -> Value.String tag
        | _ -> Value.Nothing

    let noteValue =
        match options.Note with
        | Some note -> Value.String note
        | _ -> Value.Nothing

    let terrabuildVars =
        [ "configuration", Value.String options.Configuration
          "branch_or_tag", Value.String options.BranchOrTag 
          "head_commit", Value.String options.HeadCommit.Sha
          "retry", Value.Bool options.Retry 
          "force", Value.Bool options.Force 
          "ci", Value.Bool options.Run.IsSome 
          "debug", Value.Bool options.Debug 
          "tag", tagValue 
          "note", noteValue ]
        |> Map  


    let evaluationContext =
        { Eval.EvaluationContext.WorkspaceDir = Some options.Workspace
          Eval.EvaluationContext.ProjectDir = None
          Eval.EvaluationContext.Versions = Map.empty
          Eval.EvaluationContext.Data = Map [ "terrabuild", Value.Map terrabuildVars ] }


    // bind variables
    let variables =
        let convertToVarType (name: string) (defaultValue: Value option) (value: string) =
            match value, defaultValue with
            | Bool value, Some (Value.Bool _) -> Value.Bool value
            | Bool value, None -> Value.Bool value
            | Number value, Some (Value.Number _) -> Value.Number value
            | Number value, None -> Value.Number value
            | String value, _ -> Value.String value
            | _ -> raiseTypeError $"Value '{value}' can't be converted to variable {name}"

        workspaceConfig.Variables
        |> Map.map (fun name expr ->
            // find dependencies for expression - it must have *no* dependencies for evaluation
            let defaultValue =
                match expr with
                | None -> None
                | Some expr ->
                    let deps = Dependencies.find expr
                    if deps <> Set.empty then raiseInvalidArg "Default value for variable {name} must have no dependencies"
                    expr |> Eval.eval evaluationContext |> Some

            let value =
                match $"TB_VAR_{name}" |> Environment.GetEnvironmentVariable with
                | null ->
                    match options.Variables |> Map.tryFind name with
                    | None -> defaultValue
                    | Some value -> convertToVarType name defaultValue value |> Some
                | value -> convertToVarType name defaultValue value |> Some

            match value with
            | Some expr -> expr
            | _ -> raiseInvalidArg $"Variable {name} is not initialized")

    let evaluationContext = { evaluationContext with Data = evaluationContext.Data |> Map.add "var" (Value.Map variables) }



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
                let script =
                    ext.Script
                    |> Option.bind (Eval.asStringOption << Eval.eval evaluationContext)
                match script with
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
                try
                    FrontEnd.Project.parse projectContent
                with exn ->
                    raiseParserError($"Failed to read PROJECT configuration '{projectId}'", exn)
            | _ ->
                raiseInvalidArg $"No PROJECT found in directory '{projectFile}'"

        let extensions = extensions |> Map.addMap projectConfig.Extensions

        let projectScripts =
            projectConfig.Extensions
            |> Map.map (fun _ ext ->
                ext.Script
                |> Option.bind (Eval.asStringOption << Eval.eval evaluationContext)
                |> Option.map (FS.workspaceRelative options.Workspace projectDir))

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
                | Extensions.ErrorTarget exn -> forwardExternalError($"Invocation failure of command '__defaults__' for extension '{init}'", exn)
            | _ -> ProjectInfo.Default

        let evalAsStringSet expr =
            expr
            |> Option.bind (Eval.asStringSetOption << Eval.eval evaluationContext)
            |> Option.defaultValue Set.empty

        let projectIgnores = projectConfig.Project.Ignores |> evalAsStringSet
        let projectOutputs = projectConfig.Project.Outputs |> evalAsStringSet
        let projectDependencies = projectConfig.Project.Dependencies |> evalAsStringSet
        let projectLinks = projectConfig.Project.Links |> evalAsStringSet
        let projectIncludes = projectConfig.Project.Includes |> evalAsStringSet
        let labels = projectConfig.Project.Labels

        let projectInfo = {
            projectInfo
            with Ignores = projectInfo.Ignores + projectIgnores
                 Outputs = projectInfo.Outputs + projectOutputs
                 Dependencies = projectInfo.Dependencies + projectDependencies
                 Links = projectInfo.Links + projectLinks
                 Includes = projectInfo.Includes + projectIncludes }

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

        let locals =
            workspaceConfig.Locals
            |> Map.iter (fun name _ ->
                if projectConfig.Locals |> Map.containsKey name then raiseParseError $"Duplicated local: {name}")
            workspaceConfig.Locals |> Map.addMap projectConfig.Locals

        { LoadedProject.Dependencies = projectDependencies
          LoadedProject.Links = projectLinks
          LoadedProject.Includes = includes
          LoadedProject.Ignores = projectIgnores
          LoadedProject.Outputs = projectOutputs
          LoadedProject.Targets = projectTargets
          LoadedProject.Labels = labels
          LoadedProject.Extensions = extensions
          LoadedProject.Scripts = scripts
          LoadedProject.Locals = locals }


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
                    let mutable evaluationContext =
                        let actionVariables =
                            Map [ "project", Value.String projectId
                                  "target" , Value.String targetName
                                  "hash", Value.String projectHash ]
                        let actionVariables =
                            evaluationContext.Data["terrabuild"] |> Eval.asMap
                            |> Map.addMap actionVariables
                            |>  Value.Map

                        { evaluationContext with
                            Eval.ProjectDir = Some projectDir
                            Eval.Versions = versions
                            Eval.Data = evaluationContext.Data |> Map.add "terrabuild" actionVariables |> Map.add "local" Value.EmptyMap }

                    // build the values
                    let localsHub = Hub.Create(1)

                    // bootstrap
                    for (KeyValue(scopeName, scopeValue)) in evaluationContext.Data do
                        match scopeValue with
                        | Value.Map map ->
                            for (KeyValue(name, value)) in map do
                                let varName = $"{scopeName}.{name}"
                                localsHub.Subscribe varName Array.empty (fun () ->
                                    let varSignal = localsHub.GetSignal<Value> varName
                                    varSignal.Value <- value)
                        | _ -> raiseBugError "Unexpected scope content"

                    for (KeyValue(name, localExpr)) in projectDef.Locals do
                        let localName = $"local.{name}"
                        let deps = Dependencies.find localExpr
                        let signalDeps =
                            deps
                            |> Seq.map (fun dep -> localsHub.GetSignal<Value> dep :> ISignal)
                            |> Array.ofSeq
                        localsHub.Subscribe localName signalDeps (fun () ->
                            let value = Eval.eval evaluationContext localExpr
                            let localMap =
                                match evaluationContext.Data["local"] with
                                | Value.Map map ->
                                    map |> Map.add name value
                                | _ -> raiseBugError "Unexpected scope content"

                            evaluationContext <- { evaluationContext with Data = evaluationContext.Data |> Map.add "local" (Value.Map localMap) }

                            let localSignal = localsHub.GetSignal<Value> localName
                            localSignal.Value <- value)

                    match localsHub.WaitCompletion() with
                    | Status.Ok -> evaluationContext
                    | Status.UnfulfilledSubscription (subscription, signals) ->
                        let unraisedSignals = signals |> String.join ","
                        raiseInvalidArg $"Failed to evaluate '{subscription}': a local value with the name '{unraisedSignals}' has not been declared."
                    | Status.SubscriptionError exn ->
                        forwardExternalError("Failed to evaluate locals", exn)

                // use value from project target
                // otherwise use workspace target
                // defaults to allow caching
                let rebuild =
                    let rebuild =
                        match target.Rebuild with
                        | Some rebuild -> Some rebuild
                        | _ ->
                            workspaceConfig.Targets
                            |> Map.tryFind targetName
                            |> Option.bind _.Rebuild
                    rebuild
                    |> Option.bind (Eval.asBoolOption << Eval.eval evaluationContext)
                    |> Option.defaultValue false

                let targetOperations =
                    target.Steps
                    |> List.fold (fun actions step ->
                        let extension = 
                            match projectDef.Extensions |> Map.tryFind step.Extension with
                            | Some extension -> extension
                            | _ -> raiseSymbolError $"Extension {step.Extension} is not defined"

                        let context =
                            extension.Defaults |> Option.defaultValue Map.empty
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

                        let extVariables =
                            extension.Variables
                            |> Option.bind (Eval.asStringSetOption << Eval.eval evaluationContext)
                            |> Option.defaultValue Set.empty

                        let hash =
                            let containerInfos = 
                                match container with
                                | Some container -> [ container ] @ List.ofSeq extVariables
                                | _ -> []

                            let platformInfos = 
                                match platform with
                                // TODO: why extVariables ??? seems useless
                                | Some platform -> [ platform ] @ List.ofSeq extVariables
                                | _ -> []

                            [ step.Extension; step.Command ] @ containerInfos @ platformInfos
                            |> Hash.sha256strings

                        let targetContext = {
                            TargetOperation.Hash = hash
                            TargetOperation.Container = container
                            TargetOperation.Platform = platform
                            TargetOperation.ContainerVariables = extVariables
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
                    let targetOutputs =
                        target.Outputs
                        |> Option.bind (Eval.asStringSetOption << Eval.eval evaluationContext)
                    match targetOutputs with
                    | Some outputs -> outputs
                    | _ -> projectDef.Outputs

                let hash =
                    targetOperations
                    |> List.map (fun ope -> ope.Hash)
                    |> Hash.sha256strings

                let targetCache =
                    let targetCache =
                        target.Cache
                        |> Option.bind (Eval.asStringOption << Eval.eval evaluationContext)
                    match targetCache with
                    | Some "never" -> Some Cacheability.Never
                    | Some "local" -> Some Cacheability.Local
                    | Some "remote" -> Some Cacheability.Remote
                    | Some "always" -> Some Cacheability.Always
                    | None -> None
                    | _ -> raiseParseError "invalid cache value"

                let target = {
                    Target.Hash = hash
                    Target.Rebuild = rebuild
                    Target.DependsOn = dependsOn
                    Target.Cache = targetCache
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
        let workspaceIgnores = workspaceConfig.Workspace.Ignores |> Option.defaultValue Set.empty
        let scanFolder = scanFolders options.Workspace workspaceIgnores
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
            forwardExternalError("Failed to load configuration", exn)


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

    let workspaceId = workspaceConfig.Workspace.Id

    let targets =
        workspaceConfig.Targets
        |> Map.map (fun _ target -> target.DependsOn)

    { Workspace.Id = workspaceId
      Workspace.SelectedProjects = selectedProjects
      Workspace.Projects = projects |> Map.ofDict
      Workspace.Targets = targets }
