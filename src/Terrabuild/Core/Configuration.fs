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
open Terrabuild.Configuration

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
    Id: string option
    Path: string
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
type private LoadedProject =
    { Id: string option
      Path: string
      DependsOn: string set
      Dependencies: string set
      Includes: string set
      Ignores: string set
      Outputs: string set
      Targets: Map<string, AST.Project.TargetBlock>
      Labels: string set
      Extensions: Map<string, AST.ExtensionBlock>
      Scripts: Map<string, LazyScript>
      Locals: Map<string, Expr> }




type IWorkspaceService =
    abstract member GetProjectById: string -> Project
    abstract member GetProjectByPath: string -> Project
    abstract member Completed: Project -> unit



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




let private buildEvaluationContext (options: ConfigOptions.Options) (workspaceConfig: AST.Workspace.WorkspaceFile) =
    let tagValue = 
        match options.Tag with
        | Some tag -> Value.String tag
        | _ -> Value.Nothing

    let noteValue =
        match options.Note with
        | Some note -> Value.String note
        | _ -> Value.Nothing

    let terrabuildVars =
        Map [ "terrabuild.configuration", Value.String options.Configuration
              "terrabuild.branch_or_tag", Value.String options.BranchOrTag 
              "terrabuild.head_commit", Value.String options.HeadCommit.Sha
              "terrabuild.retry", Value.Bool options.Retry 
              "terrabuild.force", Value.Bool options.Force 
              "terrabuild.ci", Value.Bool options.Run.IsSome 
              "terrabuild.debug", Value.Bool options.Debug 
              "terrabuild.tag", tagValue 
              "terrabuild.note", noteValue ]
 
    let evaluationContext =
        { Eval.EvaluationContext.WorkspaceDir = Some options.Workspace
          Eval.EvaluationContext.ProjectDir = None
          Eval.EvaluationContext.Versions = Map.empty
          Eval.EvaluationContext.Data = terrabuildVars }


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

        let varResolver name = raiseSymbolError $"Variable {name} is not defined"

        workspaceConfig.Variables
        |> Map.map (fun name expr ->
            // find dependencies for expression - it must have *no* dependencies for evaluation
            let defaultValue =
                match expr with
                | None -> None
                | Some expr ->
                    let deps = Dependencies.find expr
                    if deps <> Set.empty then raiseInvalidArg "Default value for variable {name} must have no dependencies"
                    expr |> Eval.eval varResolver evaluationContext |> Some

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
        |> Seq.map (fun (KeyValue(name, expr)) -> $"var.{name}", expr)
        |> Map.ofSeq

    { evaluationContext with
        Data = evaluationContext.Data |> Map.addMap variables }


let private buildScripts (options: ConfigOptions.Options) (workspaceConfig: AST.Workspace.WorkspaceFile) evaluationContext =
    // load system extensions
    let sysScripts =
        Extensions.systemExtensions
        |> Map.map (fun _ _ -> None)
        |> Map.map Extensions.lazyLoadScript

    let varResolver name = raiseSymbolError $"Variable {name} is not defined"

    // load user extension
    let usrScripts =
        workspaceConfig.Extensions
        |> Map.map (fun _ ext ->
            let script =
                ext.Script
                |> Option.bind (Eval.asStringOption << Eval.eval varResolver evaluationContext)
            match script with
            | Some script -> script |> FS.workspaceRelative options.Workspace "" |> Some
            | _ -> None)
        |> Map.map Extensions.lazyLoadScript

    let scripts = sysScripts |> Map.addMap usrScripts
    scripts





// this is the final stage: create targets and create the project
let private finalizeProject evaluationContext (projectDef: LoadedProject) (workspaceService: IWorkspaceService) =
    let projectId = projectDef.Path |> String.toUpper
    let tbFiles = Set [ "WORKSPACE"; "PROJECT" ]

    let projectDependencies =
        projectDef.DependsOn
        |> Seq.map (fun dependency -> dependency, workspaceService.GetProjectByPath dependency)
        |> Map.ofSeq

    // get dependencies on files
    let files =
        projectDef.Path
        |> IO.enumerateFilesBut projectDef.Includes (projectDef.Outputs + projectDef.Ignores + tbFiles)
        |> Set

    let filesHash =
        files
        |> Seq.sort
        |> Hash.sha256files

    let dependenciesHash =
        let versionDependencies =
            projectDependencies
            |> Map.filter (fun projectId _ -> Set.contains projectId projectDef.Dependencies)
            |> Map.map (fun _ depProj -> depProj.Hash)

        versionDependencies.Values
        |> Seq.sort
        |> Hash.sha256strings

    let versions = 
        projectDependencies
        |> Map.map (fun _ depProj -> depProj.Hash)

    // NOTE: this is the hash (modulo target name) used for reconcialiation across executions
    let projectHash =
        [ projectId; filesHash; dependenciesHash ]
        |> Hash.sha256strings

    let varResolver varName =
        let project = workspaceService.GetProjectById varName
        [ "version", Value.String project.Hash ] |> Map.ofSeq |> Value.Map

    let projectSteps =
        projectDef.Targets
        |> Map.map (fun targetName target ->

            let evaluationContext =
                let mutable evaluationContext =
                    let terrabuildProjectVars =
                        Map [ "terrabuild.project", Value.String projectId
                              "terrabuild.target" , Value.String targetName
                              "terrabuild.hash", Value.String projectHash ]

                    let projectVars =
                        projectDependencies
                        |> Seq.choose (fun (KeyValue(_, project)) ->
                            project.Id |> Option.map (fun id ->
                                $"project.{id}", Value.Map (Map ["version", Value.String project.Hash])))
                        |> Map.ofSeq

                    { evaluationContext with
                        Eval.ProjectDir = Some projectDef.Path
                        Eval.Versions = versions
                        Eval.Data =
                            evaluationContext.Data
                            |> Map.addMap terrabuildProjectVars
                            |> Map.addMap projectVars }

                // build the values
                let localsHub = Hub.Create(1)

                // bootstrap
                for (KeyValue(name, value)) in evaluationContext.Data do
                    localsHub.Subscribe name [] (fun () ->
                        let varSignal = localsHub.GetSignal<Value> name
                        varSignal.Value <- value)

                for (KeyValue(name, localExpr)) in projectDef.Locals do
                    let localName = $"local.{name}"
                    let deps = Dependencies.find localExpr
                    let signalDeps =
                        deps
                        |> Seq.map (fun dep -> localsHub.GetSignal<Value> dep :> ISignal)
                        |> List.ofSeq
                    localsHub.Subscribe localName signalDeps (fun () ->
                        let localValue = Eval.eval varResolver evaluationContext localExpr
                        evaluationContext <- { evaluationContext with Data = evaluationContext.Data |> Map.add localName localValue }
                        let localSignal = localsHub.GetSignal<Value> localName
                        localSignal.Value <- localValue)

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
                target.Rebuild
                |> Option.bind (Eval.asBoolOption << Eval.eval varResolver evaluationContext)
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
                        |> Eval.eval varResolver evaluationContext

                    let container =
                        match extension.Container with
                        | Some container ->
                            match Eval.eval varResolver evaluationContext container with
                            | Value.String container -> Some container
                            | Value.Nothing -> None
                            | _ -> raiseTypeError "container must evaluate to a string"
                        | _ -> None

                    let platform =
                        match extension.Platform with
                        | Some platform ->
                            match Eval.eval varResolver evaluationContext platform with
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
                        |> Option.bind (Eval.asStringSetOption << Eval.eval varResolver evaluationContext)
                        |> Option.defaultValue Set.empty

                    let hash =
                        let containerDeps =
                            match container with
                            | Some container ->
                                let lstVariables = extVariables |> List.ofSeq |> List.sort
                                let lstPlatform = platform |> Option.map (fun p -> [ p ]) |> Option.defaultValue []
                                container :: lstVariables @ lstPlatform
                            | _ -> []

                        [ step.Extension; step.Command ] @ containerDeps
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

            let dependsOn = target.DependsOn |> Option.defaultValue Set.empty

            let outputs =
                let targetOutputs =
                    target.Outputs
                    |> Option.bind (Eval.asStringSetOption << Eval.eval varResolver evaluationContext)
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
                    |> Option.bind (Eval.asStringOption << Eval.eval varResolver evaluationContext)
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

    let files = files |> Set.map (FS.relativePath projectDef.Path)

    let projectDependencies = projectDependencies.Keys |> Seq.map String.toUpper |> Set.ofSeq

    let project =
        { Project.Id = projectDef.Id
          Project.Path = projectDef.Path
          Project.Hash = projectHash
          Project.Dependencies = projectDependencies
          Project.Files = files
          Project.Targets = projectSteps
          Project.Labels = projectDef.Labels }
    workspaceService.Completed project



// this is the first stage: load project and mostly get dependencies references
let private loadProjectDef (options: ConfigOptions.Options) (workspaceService: IWorkspaceService) (workspaceConfig: AST.Workspace.WorkspaceFile) evaluationContext extensions scripts projectPath =
    let projectDir = FS.combinePath options.Workspace projectPath
    let projectFile = FS.combinePath projectDir "PROJECT"

    Log.Debug("Loading project definition {ProjectId}", projectPath)

    let varResolver varName =
        let project = workspaceService.GetProjectById varName
        [ "version", Value.String project.Hash ] |> Map.ofSeq |> Value.Map

    let projectConfig =
        match projectFile with
        | FS.File projectFile ->
            let projectContent = File.ReadAllText projectFile
            Terrabuild.Configuration.FrontEnd.Project.parse projectContent
        | _ ->
            raiseInvalidArg $"No PROJECT found in directory '{projectFile}'"

    let projectExtensions = extensions |> Map.addMap projectConfig.Extensions

    let projectExtensionScripts =
        let declaredScripts =
            projectConfig.Extensions
            |> Map.map (fun _ ext ->
                ext.Script
                |> Option.bind (Eval.asStringOption << Eval.eval varResolver evaluationContext)
                |> Option.map (FS.workspaceRelative options.Workspace projectDir))

        scripts
        |> Map.addMap (declaredScripts |> Map.map Extensions.lazyLoadScript)

    let projectInfo =
        match projectConfig.Project.Init with
        | Some init ->
            let parseContext = 
                let context = { Terrabuild.Extensibility.ExtensionContext.Debug = options.Debug
                                Terrabuild.Extensibility.ExtensionContext.Directory = projectDir
                                Terrabuild.Extensibility.ExtensionContext.CI = options.Run.IsSome }
                Value.Map (Map [ "context", Value.Object context ])

            let result =
                Extensions.getScript init projectExtensionScripts
                |> Extensions.invokeScriptMethod<ProjectInfo> "__defaults__" parseContext

            match result with
            | Extensions.Success result -> result
            | Extensions.ScriptNotFound -> raiseSymbolError $"Script {init} was not found"
            | Extensions.TargetNotFound -> ProjectInfo.Default // NOTE: if __defaults__ is not found - this will silently use default configuration, probably emit warning
            | Extensions.ErrorTarget exn -> forwardExternalError($"Invocation failure of command '__defaults__' for extension '{init}'", exn)
        | _ -> ProjectInfo.Default

    let evalAsStringSet expr =
        expr
        |> Option.bind (Eval.asStringSetOption << Eval.eval varResolver evaluationContext)
        |> Option.defaultValue Set.empty

    let dependsOn =
        // collect dependencies for all the project
        // NOTE we are keeping only project dependencies as we want to construct project graph
        projectConfig.Project.DependsOn |> Option.defaultValue Set.empty
        |> Set.union (Dependencies.reflectionFind projectConfig)
        |> Set.choose (fun dep -> if dep.StartsWith("project.") then Some dep else None)

    let projectIgnores = projectConfig.Project.Ignores |> evalAsStringSet
    let projectOutputs = projectConfig.Project.Outputs |> evalAsStringSet
    let projectDependencies = projectConfig.Project.Dependencies |> evalAsStringSet
    let projectIncludes = projectConfig.Project.Includes |> evalAsStringSet
    let projectLabels = projectConfig.Project.Labels

    let projectInfo = {
        projectInfo with
            Ignores = projectInfo.Ignores + projectIgnores
            Outputs = projectInfo.Outputs + projectOutputs
            Dependencies = projectInfo.Dependencies + projectDependencies
            Includes = projectInfo.Includes + projectIncludes }

    let projectOutputs = projectInfo.Outputs
    let projectIgnores = projectInfo.Ignores

    // convert relative dependencies to absolute dependencies respective to workspaceDirectory
    let projectDependencies =
        projectInfo.Dependencies
        |> Set.map (fun dep -> FS.workspaceRelative options.Workspace projectDir dep)

    let projectTargets =
        projectConfig.Targets
        |> Map.map (fun targetName targetBlock ->
            let workspaceTarget = workspaceConfig.Targets |> Map.tryFind targetName
            let rebuild =
                match targetBlock.Rebuild with
                | Some expr -> Some expr
                | _ -> workspaceTarget |> Option.bind _.Rebuild
            let dependsOn =
                match targetBlock.DependsOn with
                | Some dependsOn -> Some dependsOn
                | _ -> workspaceTarget |> Option.bind _.DependsOn

            { targetBlock with 
                Rebuild = rebuild
                DependsOn = dependsOn })

    let projectIncludes = projectInfo.Includes

    // enrich workspace locals with project locals
    // NOTE we are checking for duplicated fields as this is an error
    let locals =
        workspaceConfig.Locals
        |> Map.iter (fun name _ ->
            if projectConfig.Locals |> Map.containsKey name then raiseParseError $"Duplicated local: {name}")
        workspaceConfig.Locals |> Map.addMap projectConfig.Locals

    let loadedProject =
        { LoadedProject.Id = projectConfig.Project.Id
          LoadedProject.Path = projectPath
          LoadedProject.DependsOn = dependsOn
          LoadedProject.Dependencies = projectDependencies
          LoadedProject.Includes = projectIncludes
          LoadedProject.Ignores = projectIgnores
          LoadedProject.Outputs = projectOutputs
          LoadedProject.Targets = projectTargets
          LoadedProject.Labels = projectLabels
          LoadedProject.Extensions = projectExtensions
          LoadedProject.Scripts = projectExtensionScripts
          LoadedProject.Locals = locals }
    loadedProject





type Protocol =
    | LoadProject of projectPath:string
    | ProjectLoaded of project:Project
    | GetProjectById of projectId:string * AsyncReplyChannel<Project>
    | GetProjectByPath of projectPath:string * AsyncReplyChannel<Project>
    | Exit



type WorkspaceLoader(options: ConfigOptions.Options) as this =

    let workspaceContent = FS.combinePath options.Workspace "WORKSPACE" |> File.ReadAllText
    let workspaceConfig =
        try
            FrontEnd.Workspace.parse workspaceContent
        with exn ->
            raiseParserError("Failed to read WORKSPACE configuration file", exn)

    let evaluationContext = buildEvaluationContext options workspaceConfig

    let scripts = buildScripts options workspaceConfig evaluationContext

    let extensions = Extensions.systemExtensions |> Map.addMap workspaceConfig.Extensions

    let hub = Hub.Create options.MaxConcurrency

    let projectLoading = ConcurrentDictionary<string, bool>()
    let projectIds = ConcurrentDictionary<string, string>()
    let projects = ConcurrentDictionary<string, Project>()

    let agent =
        let handler (inbox: MailboxProcessor<Protocol>) =
            let rec messageLoop () = async {
                let! msg = inbox.Receive()
                match msg with
                // load project asynchronously if not already loaded
                | LoadProject projectPath ->
                    let projectName = projectPath |> String.toUpper
                    if projectLoading.TryAdd(projectName, true) then
                        async {
                            let projectDef = loadProjectDef options this workspaceConfig evaluationContext extensions scripts projectPath
                            let project = finalizeProject evaluationContext projectDef this
                            project |> ignore
                        } |> Async.StartImmediate
                // project is loaded
                | ProjectLoaded project ->
                    if projects.TryAdd(project.Path, project) |> not then raiseBugError "Unexpected error"
                    let signal = hub.GetSignal<Project> project.Path
                    signal.Value <- project
                    project.Id |> Option.iter (fun id ->
                        if projectIds.TryAdd(id, project.Path) |> not then
                            raiseSymbolError $"Project id '{id}' is already defined in project '{projectIds[id]}'"
                        let signal = hub.GetSignal<Project> $"project.{id}"
                        signal.Value <- project)
                | GetProjectById (projectId, reply) ->
                    let signal = hub.GetSignal<Project> projectId
                    hub.Subscribe projectId [signal] (fun () -> reply.Reply signal.Value)
                | GetProjectByPath (projectPath, reply) ->
                    let projectPath = projectPath |> String.toUpper
                    let signal = hub.GetSignal<Project> projectPath
                    hub.Subscribe projectPath [signal] (fun () -> reply.Reply signal.Value)
                | Exit -> ()

                if msg <> Exit then return! messageLoop ()
            }
            messageLoop()
        MailboxProcessor.Start(handler)

    do
        // scan folders to discover projects
        let rec findProjects (dir: string) =
            let projectFile = FS.combinePath dir "PROJECT"
            match projectFile with
            | FS.File _ ->
                let projectPath = FS.relativePath options.Workspace dir
                let projectName = projectPath |> String.toUpper
                if projectLoading.TryAdd(projectName, true) then
                    let signal = hub.GetSignal<Project> $"load.{projectName}"
                    hub.Subscribe signal.Name [] (fun () ->
                        let projectDef = loadProjectDef options this workspaceConfig evaluationContext extensions scripts projectPath
                        let project = finalizeProject evaluationContext projectDef this
                        project |> ignore)


                projectPath |> LoadProject |> agent.Post
            | _ -> dir |> IO.enumerateDirs |> Seq.iter findProjects
        findProjects options.Workspace


    member _.WaitCompletion() =
        let status = hub.WaitCompletion()
        agent.Post Exit

        match status with
        | Status.Ok -> ()
        | Status.UnfulfilledSubscription (subscription, signals) ->
            let unraisedSignals = signals |> String.join ","
            raiseInvalidArg $"Project '{subscription}' has pending operations on '{unraisedSignals}'. Check for circular dependencies."
        | Status.SubscriptionError exn ->
            forwardExternalError("Failed to load configuration", exn)

    member _.Projects = projects

    member _.WorkspaceConfig = workspaceConfig

    interface IWorkspaceService with
        member _.GetProjectById projectId =
            agent.PostAndReply (fun reply -> GetProjectById (projectId, reply))

        member _.GetProjectByPath projectPath =
            agent.PostAndReply (fun reply -> GetProjectByPath (projectPath, reply))

        member _.Completed project =
            agent.Post (ProjectLoaded project)



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


    let workspaceLoader = WorkspaceLoader(options)
    workspaceLoader.WaitCompletion()

    let projects = workspaceLoader.Projects
    let workspaceConfig = workspaceLoader.WorkspaceConfig

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
        |> Map.map (fun _ target -> target.DependsOn |> Option.defaultValue Set.empty)

    { Workspace.Id = workspaceId
      Workspace.SelectedProjects = selectedProjects
      Workspace.Projects = projects |> Map.ofDict
      Workspace.Targets = targets }
