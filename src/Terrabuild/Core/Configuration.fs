module Configuration
open System.IO
open Collections
open System
open System.Collections.Concurrent
open Terrabuild.Extensibility
open Terrabuild.Configuration.AST
open Terrabuild.Expressions

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
    Id: string
    Hash: string
    Dependencies: string set
    Files: string set
    Ignores: string set
    Outputs: string set
    Targets: Map<string, ContaineredTarget>
    Labels: string set
}

[<RequireQualifiedAccess>]
type WorkspaceConfig = {
    Storage: Storages.Storage
    SourceControl: SourceControls.SourceControl
    Directory: string
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






module ExtensionLoaders =
    open Terrabuild.Scripting

    type InvocationResult<'t> =
        | Success of 't
        | ScriptNotFound
        | TargetNotFound
        | ErrorTarget of Exception

    // well-know provided extensions
    // do not forget to add reference when adding new implementation
    let systemScripts =
        Map [
            "@docker", typeof<Terrabuild.Extensions.Docker>
            "@dotnet", typeof<Terrabuild.Extensions.Dotnet>
            "@make", typeof<Terrabuild.Extensions.Make>
            "@npm", typeof<Terrabuild.Extensions.Npm>
            "@null", typeof<Terrabuild.Extensions.Null>
            "@shell", typeof<Terrabuild.Extensions.Shell>
            "@terraform", typeof<Terrabuild.Extensions.Terraform>
        ]

    let systemExtensions =
        systemScripts |> Map.map (fun _ _ -> Extension.Empty)


    let loadStorage name : Storages.Storage =
        match name with
        | None -> Storages.Local()
        | Some "azureblob" -> Storages.AzureBlobStorage()
        | _ -> failwith $"Unknown storage '{name}'"

    let loadSourceControl name: SourceControls.SourceControl =
        match name with
        | None -> SourceControls.Local()
        | Some "github" -> SourceControls.GitHub()
        | _ -> failwith $"Unknown source control '{name}'"

    // NOTE: when app in package as a single file, this break - so instead of providing 
    //       Terrabuild.Extensibility assembly, the Terrabuild main assembly is provided
    //       ¯\_(ツ)_/¯
    let terrabuildDir = Diagnostics.Process.GetCurrentProcess().MainModule.FileName |> IO.parentDirectory
    let terrabuildExtensibility =
        let path = IO.combinePath terrabuildDir "Terrabuild.Extensibility.dll"
        if File.Exists(path) then path
        else Reflection.Assembly.GetExecutingAssembly().Location

    let lazyLoadScript (name: string) (ext: Extension) =
        let initScript () =
            match ext.Script with
            | Some script -> loadScript [ terrabuildExtensibility ] script
            | _ ->
                match systemScripts |> Map.tryFind name with
                | Some sysTpe -> Script(sysTpe)
                | _ -> failwith $"Script is not defined for extension '{name}'"

        lazy(initScript())

    let invokeScriptMethod<'r> (scripts: Map<string, Lazy<Script>>) (extension: string) (method: string) (args: Value) =
        match scripts |> Map.tryFind extension with
        | None -> ScriptNotFound

        | Some extInstance ->
            let rec invokeScriptMethod (method: string) =
                let script = extInstance.Value
                let invocable = script.GetMethod(method)
                match invocable with
                | Some invocable ->
                    try
                        Success (invocable.Invoke<'r> args)
                    with
                    | exn -> ErrorTarget exn
                | None ->
                    match method with
                    | "__init__"
                    | "__defaults__" 
                    | "__dispatch__"-> TargetNotFound
                    | _ -> invokeScriptMethod "__dispatch__"

            invokeScriptMethod method


let read workspaceDir (options: Options) environment labels variables =
    let workspaceFile = IO.combinePath workspaceDir "WORKSPACE"
    let workspaceContent = File.ReadAllText workspaceFile
    let workspaceConfig =
        try
            Terrabuild.Configuration.FrontEnd.parseWorkspace workspaceContent
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
        ExtensionLoaders.systemExtensions
        |> Map.addMap workspaceConfig.Extensions

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
                    |> Map.addMap (projectConfig.Extensions |> Map.map ExtensionLoaders.lazyLoadScript)

                let projectInfo =
                    match projectConfig.Configuration.Init with
                    | Some init ->
                        let parseContext = 
                            let context = { Terrabuild.Extensibility.InitContext.Debug = options.Debug
                                            Terrabuild.Extensibility.InitContext.Directory = projectDir
                                            Terrabuild.Extensibility.InitContext.CI = sourceControl.CI }
                            Value.Map (Map [ "context", Value.Object context ])
                        
                        let result = ExtensionLoaders.invokeScriptMethod<Terrabuild.Extensibility.ProjectInfo> scripts init "__init__" parseContext
                        match result with
                        | ExtensionLoaders.Success result -> result
                        | ExtensionLoaders.ScriptNotFound -> ConfigException.Raise $"Script {init} was not found"
                        | ExtensionLoaders.TargetNotFound -> ProjectInfo.Default // NOTE: if __init__ is not found - this will silently use default configuration, probably emit warning
                        | ExtensionLoaders.ErrorTarget exn -> ConfigException.Raise $"Invocation failure of __init__ of script {init}" exn
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
                                let actionContext = { Terrabuild.Extensibility.ActionContext.Debug = options.Debug
                                                      Terrabuild.Extensibility.ActionContext.Directory = projectDir
                                                      Terrabuild.Extensibility.ActionContext.CI = sourceControl.CI
                                                      Terrabuild.Extensibility.ActionContext.NodeHash = nodeHash
                                                      Terrabuild.Extensibility.ActionContext.Command = step.Command
                                                      Terrabuild.Extensibility.ActionContext.BranchOrTag = branchOrTag }

                                let actionVariables =
                                    buildVariables
                                    |> Map.add "terrabuild_project" project
                                    |> Map.add "terrabuild_target" targetName

                                let stepParameters =
                                    extension.Defaults
                                    |> Map.addMap step.Parameters
                                    |> Map.add "context" (Expr.Object actionContext)
                                    |> Expr.Map
                                    |> Eval.eval actionVariables

                                let actionGroup =
                                    let result = ExtensionLoaders.invokeScriptMethod<Terrabuild.Extensibility.ActionBatch> projectDef.Scripts
                                                                                                                           step.Extension 
                                                                                                                           step.Command
                                                                                                                           stepParameters
                                    match result with
                                    | ExtensionLoaders.Success result -> result
                                    | ExtensionLoaders.ScriptNotFound -> ConfigException.Raise $"Script {step.Extension} was not found"
                                    | ExtensionLoaders.TargetNotFound -> ConfigException.Raise $"Script {step.Extension} has no function {step.Command}"
                                    | ExtensionLoaders.ErrorTarget exn -> ConfigException.Raise $"Invocation failure of {step.Command} of script {step.Extension}" exn


                                actionGroup.Actions
                                |> List.map (fun action -> { ContaineredAction.Container = extension.Container
                                                             ContaineredAction.Command = action.Command
                                                             ContaineredAction.Arguments = action.Arguments
                                                             ContaineredAction.Cache = actionGroup.Cache })
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

            let files =
                files
                |> Set.map (IO.relativePath projectDir)

            let projectConfig =
                { Project.Id = project
                  Project.Hash = nodeHash
                  Project.Dependencies = projectDef.Dependencies
                  Project.Files = files
                  Project.Outputs = projectDef.Outputs
                  Project.Ignores = projectDef.Ignores
                  Project.Targets = projectSteps
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
      WorkspaceConfig.Projects = projects
      WorkspaceConfig.Targets = workspaceConfig.Targets
      WorkspaceConfig.Environment = environment
      WorkspaceConfig.SourceControl = sourceControl
      WorkspaceConfig.Storage = storage }
