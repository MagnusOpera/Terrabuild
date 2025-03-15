module GraphBuilder
open Collections
open System.Collections.Concurrent
open Errors
open Serilog
open System
open GraphDef
open Terrabuild.Extensibility



// build the high-level graph from configuration
let build (options: ConfigOptions.Options) (configuration: Configuration.Workspace) =
    let startedAt = DateTime.UtcNow
    Log.Debug("===== [Graph Build] =====")

    $"{Ansi.Emojis.gear} Building graph" |> Terminal.writeLine

    let processedNodes = ConcurrentDictionary<string, bool>()
    let allNodes = ConcurrentDictionary<string, Node>()

    // first check all targets exist in WORKSPACE
    match options.Targets |> Seq.tryFind (fun targetName -> configuration.Targets |> Map.containsKey targetName |> not) with
    | Some undefinedTarget -> raiseSymbolError $"Target {undefinedTarget} is not defined in WORKSPACE"
    | _ -> ()


    let rec buildTarget targetName project =
        let projectConfig = configuration.Projects[project]
        let nodeId = $"{project}:{targetName}"

        let processNode () =
            // merge targets requirements
            let buildDependsOn =
                configuration.Targets
                |> Map.tryFind targetName
                |> Option.map (fun ct -> ct.DependsOn)
                |> Option.defaultValue Set.empty
            let projDependsOn =
                projectConfig.Targets
                |> Map.tryFind targetName
                |> Option.map (fun ct -> ct.DependsOn)
                |> Option.defaultValue Set.empty
            let dependsOns = buildDependsOn + projDependsOn

            // apply on each dependency
            let children, hasInternalDependencies =
                dependsOns
                |> Set.fold (fun (acc, hasInternalDependencies) dependsOn ->
                    let childDependencies, hasInternalDependencies =
                        match dependsOn with
                        | String.Regex "^\^(.+)$" [ parentDependsOn ] ->
                            projectConfig.Dependencies |> Set.collect (buildTarget parentDependsOn), hasInternalDependencies
                        | _ ->
                            buildTarget dependsOn project, true
                    acc + childDependencies, hasInternalDependencies) (Set.empty, false)

            // NOTE: a node is considered a leaf (within this project only) if the target has no internal dependencies detected
            let isLeaf = hasInternalDependencies |> not

            // only generate computation node - that is node that generate something
            // barrier nodes are just discarded and dependencies lift level up
            match projectConfig.Targets |> Map.tryFind targetName with
            | Some target ->
                let cache, ops =
                    target.Operations
                    |> List.fold (fun (cache, ops) operation ->
                        let optContext = {
                            Terrabuild.Extensibility.ActionContext.Debug = options.Debug
                            Terrabuild.Extensibility.ActionContext.CI = options.Run.IsSome
                            Terrabuild.Extensibility.ActionContext.Command = operation.Command
                            Terrabuild.Extensibility.ActionContext.Hash = projectConfig.Hash
                        }

                        let parameters = 
                            match operation.Context with
                            | Terrabuild.Expressions.Value.Map map ->
                                map
                                |> Map.add "context" (Terrabuild.Expressions.Value.Object optContext)
                                |> Terrabuild.Expressions.Value.Map
                            | _ -> raiseBugError "Failed to get context (internal error)"

                        Log.Debug($"{hash}: Invoking extension '{operation.Extension}::{operation.Command}' with args {parameters}")

                        let executionRequest =
                            match Extensions.invokeScriptMethod<Terrabuild.Extensibility.ActionExecutionRequest> optContext.Command parameters (Some operation.Script) with
                            | Extensions.InvocationResult.Success executionRequest -> executionRequest
                            | Extensions.InvocationResult.ErrorTarget ex -> forwardExternalError $"{hash}: Failed to get shell operation (extension error)" ex
                            | _ -> raiseExternalError $"{hash}: Failed to get shell operation (extension error)"

                        let newops =
                            executionRequest.Operations
                            |> List.map (fun shellOperation -> {
                                ContaineredShellOperation.Container = operation.Container
                                ContaineredShellOperation.ContainerPlatform = operation.Platform
                                ContaineredShellOperation.ContainerVariables = operation.ContainerVariables
                                ContaineredShellOperation.MetaCommand = $"{operation.Extension} {operation.Command}"
                                ContaineredShellOperation.Command = shellOperation.Command
                                ContaineredShellOperation.Arguments = shellOperation.Arguments })

                        let cache = cache &&& executionRequest.Cache
                        cache, ops @ newops
                    ) (Cacheability.Always, [])

                let opsCmds =
                    ops
                    |> List.map Json.Serialize

                let hashContent = opsCmds @ [
                    yield projectConfig.Hash
                    yield target.Hash
                    yield! children |> Seq.map (fun nodeId -> allNodes[nodeId].TargetHash)
                ]

                let hash = hashContent |> Hash.sha256strings

                Log.Debug($"Node {nodeId} has ProjectHash {projectConfig.Hash} and TargetHash {hash}")

                let cache = 
                    if options.Force then Cacheability.Never
                    elif options.LocalOnly then Cacheability.Local
                    else
                        let targetCache =
                            match target.Cache with
                            | Some Terrabuild.Configuration.Project.AST.Cacheability.Never -> Some Cacheability.Never
                            | Some Terrabuild.Configuration.Project.AST.Cacheability.Local -> Some Cacheability.Local
                            | Some Terrabuild.Configuration.Project.AST.Cacheability.Remote -> Some Cacheability.Remote
                            | Some Terrabuild.Configuration.Project.AST.Cacheability.Always -> Some Cacheability.Always
                            | _ -> None
                        targetCache |> Option.defaultValue cache

                let node = { Node.Id = nodeId
                             Node.Label = $"{targetName} {projectConfig.Name}"
                             
                             Node.Project = projectConfig.Name
                             Node.Target = targetName
                             Node.ConfigurationTarget = target
                             Node.Operations = ops
                             Node.Cache = cache

                             Node.Dependencies = children
                             Node.Outputs = target.Outputs

                             Node.ProjectHash = projectConfig.Hash
                             Node.TargetHash = hash

                             Node.IsLeaf = isLeaf }

                if allNodes.TryAdd(nodeId, node) |> not then raiseBugError "Unexpected graph building race"
                Set.singleton nodeId
            | _ ->
                children

        if processedNodes.TryAdd(nodeId, true) then processNode()
        else
            match allNodes.TryGetValue(nodeId) with
            | true, _ -> Set.singleton nodeId
            | _ -> Set.empty

    let rootNodes =
        configuration.SelectedProjects
        |> Seq.collect (fun dependency -> options.Targets
                                          |> Seq.collect (fun target ->
                                            buildTarget target dependency))
        |> Set

    let endedAt = DateTime.UtcNow
    let buildDuration = endedAt - startedAt
    Log.Debug("Graph Build: {duration}", buildDuration)

    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} {allNodes.Count} tasks" |> Terminal.writeLine

    { Graph.Nodes = allNodes |> Map.ofDict
      Graph.RootNodes = rootNodes }
