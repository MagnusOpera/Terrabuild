module GraphAnalysisBuilder
open Collections
open System.Collections.Concurrent
open Errors
open Serilog
open System
open GraphDef



// build the high-level graph from configuration
let build (configuration: Configuration.Workspace) (options: Configuration.Options) =
    let startedAt = DateTime.UtcNow
    $"{Ansi.Emojis.popcorn} Building graph" |> Terminal.writeLine

    let processedNodes = ConcurrentDictionary<string, bool>()
    let allNodes = ConcurrentDictionary<string, Node>()

    // first check all targets exist in WORKSPACE
    match options.Targets |> Seq.tryFind (fun targetName -> configuration.Targets |> Map.containsKey targetName |> not) with
    | Some undefinedTarget -> TerrabuildException.Raise($"Target {undefinedTarget} is not defined in WORKSPACE")
    | _ -> ()


    let rec buildTarget targetName project =
        let nodeId = $"{project}:{targetName}"

        let processNode () =
            let projectConfig = configuration.Projects[project]

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
                let hashContent = [
                    yield projectConfig.Hash
                    yield target.Hash
                    yield! children |> Seq.map (fun nodeId -> allNodes[nodeId].TargetHash)
                ]

                let hash = hashContent |> Hash.sha256strings

                let isForced =
                    let isSelectedProject = configuration.SelectedProjects |> Set.contains project
                    let isSelectedTarget = options.Targets |> Set.contains targetName
                    let forced = options.Force && isSelectedProject && isSelectedTarget || target.Rebuild
                    if forced then Log.Debug("{nodeId} must rebuild because force build is requested or target shall rebuild", nodeId)
                    forced

                let node = { Node.Id = nodeId
                             Node.Label = $"{targetName} {project}"
                             
                             Node.Project = project
                             Node.Target = targetName
                             Node.ConfigurationTarget = target
                             Node.TargetOperation = None
                             Node.ShellOperations = []

                             Node.Dependencies = children
                             Node.Outputs = target.Outputs

                             Node.ProjectHash = projectConfig.Hash
                             Node.TargetHash = hash
                             Node.OperationHash = target.Hash

                             Node.IsLeaf = isLeaf
                             Node.IsForced = isForced
                             Node.IsRequired = isForced
                             
                             Node.IsLast = true }

                if allNodes.TryAdd(nodeId, node) |> not then
                    TerrabuildException.Raise("Unexpected graph building race")
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
                                          |> Seq.collect (fun target -> buildTarget target dependency))
        |> Set

    let endedAt = DateTime.UtcNow
    let buildDuration = endedAt - startedAt
    Log.Debug("Graph Build: {duration}", buildDuration)

    { Graph.Nodes = allNodes |> Map.ofDict
      Graph.RootNodes = rootNodes }
