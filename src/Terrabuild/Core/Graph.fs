module Graph
open System
open System.Collections.Concurrent
open Collections
open Terrabuild.Extensibility
open Serilog
open Errors
open Terrabuild.PubSub

type Paths = string set


[<RequireQualifiedAccess>]
type Node = {
    Id: string
    Hash: string
    Project: string
    Target: string
    Label: string
    Dependencies: string set
    ProjectHash: string
    Outputs: string set
    IsLeaf: bool
    Forced: bool
    Required: bool
    Cluster: string

    Cache: Cacheability
    ShellOperations: ShellOperations
    ConfigOperations: Configuration.TargetOperation list
    BuildSummary: Cache.TargetSummary option
}

type Workspace = {
    Targets: string set
    Nodes: Map<string, Node>
    SelectedNodes: string set
}



let graph (graph: Workspace) =
    let clusterColors =
        graph.Nodes
        |> Seq.map (fun (KeyValue(nodeId, node)) ->
            let hash = Hash.sha256 node.Cluster
            node.Cluster, $"#{hash.Substring(0, 3)}")
        |> Map.ofSeq

    let clusters =
        graph.Nodes
        |> Seq.groupBy (fun (KeyValue(_, node)) -> node.Cluster)
        |> Map.ofSeq
        |> Map.map (fun _ v -> v |> Seq.map (fun kvp -> kvp.Value) |> List.ofSeq)

    let mermaid = [
        "flowchart LR"
        $"classDef forced stroke:red,stroke-width:3px"
        $"classDef required stroke:orange,stroke-width:3px"
        $"classDef selected stroke:black,stroke-width:3px"

        for (KeyValue(cluster, nodes)) in clusters do
            let clusterNode = nodes |> List.tryFind (fun node -> node.Id = cluster)
            let isCluster = clusterNode |> Option.isSome

            if isCluster then $"subgraph {cluster}[batch {clusterNode.Value.Target}]"

            let offset, nodes =
                if isCluster then "  ", nodes |> List.filter (fun node -> node.Id <> cluster)
                else "", nodes

            for node in nodes do
                $"{offset}{node.Hash}([{node.Label}])"

            if isCluster then
                "end"
                $"classDef cluster-{cluster} stroke:{clusterColors[cluster]},stroke-width:3px,fill:white,rx:10,ry:10"
                $"class {cluster} cluster-{cluster}"

            for srcNode in nodes do
                for dependency in srcNode.Dependencies do
                    if dependency <> cluster then
                        let dstNode = graph.Nodes |> Map.find dependency
                        $"{srcNode.Hash} --> {dstNode.Hash}"

                if srcNode.Forced then $"class {srcNode.Hash} forced"
                elif srcNode.Required then $"class {srcNode.Hash} required"
                elif graph.SelectedNodes |> Set.contains srcNode.Id then $"class {srcNode.Hash} selected"
    ]

    mermaid




let create (configuration: Configuration.Workspace) (targets: string set) (options: Configuration.Options) =
    $"{Ansi.Emojis.popcorn} Constructing graph" |> Terminal.writeLine

    let processedNodes = ConcurrentDictionary<string, bool>()
    let allNodes = ConcurrentDictionary<string, Node>()

    // first check all targets exist in WORKSPACE
    match targets |> Seq.tryFind (fun targetName -> configuration.Targets |> Map.containsKey targetName |> not) with
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
                    yield! children |> Seq.map (fun nodeId -> allNodes[nodeId].Hash)
                ]

                let hash = hashContent |> Hash.sha256strings

                let isForced =
                    let isSelectedProject = configuration.SelectedProjects |> Set.contains project
                    let isSelectedTarget = targets |> Set.contains targetName
                    let forced = options.Force && isSelectedProject && isSelectedTarget
                    if forced then Log.Debug("{nodeId} must rebuild because force build is requested", nodeId)
                    forced

                let node = { Node.Id = nodeId
                             Node.Hash = hash
                             Node.Project = project
                             Node.Target = targetName
                             Node.Label = $"{targetName} {project}"
                             Node.Outputs = target.Outputs
                             Node.Dependencies = children
                             Node.ProjectHash = projectConfig.Hash
                             Node.IsLeaf = isLeaf
                             Node.Required = false
                             Node.Forced = isForced
                             Node.Cluster = target.Hash
                             Node.Cache = Cacheability.Never
                             Node.ShellOperations = []
                             Node.ConfigOperations = target.Operations
                             Node.BuildSummary = None }

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

    let selectedNodes =
        configuration.SelectedProjects |> Seq.collect (fun dependency ->
            targets |> Seq.collect (fun target -> buildTarget target dependency))
        |> Set

    { Targets = targets
      Nodes = allNodes |> Map.ofDict
      SelectedNodes = selectedNodes }





let enforceConsistency (configuration: Configuration.Workspace) (graph: Workspace) (cache: Cache.ICache) (options: Configuration.Options) =
    let startedAt = DateTime.UtcNow

    let cacheMode =
        if configuration.SourceControl.CI then Cacheability.Always
        else Cacheability.Remote

    let allNodes = ConcurrentDictionary<string, Node>()
    let hub = Hub.Create(options.MaxConcurrency)
    for (KeyValue(nodeId, node)) in graph.Nodes do
        let nodeComputed = hub.CreateComputed<bool*DateTime> nodeId

        // await dependencies
        let awaitedDependencies =
            node.Dependencies
            |> Seq.map (fun awaitedProjectId -> hub.GetComputed<bool*DateTime> awaitedProjectId)
            |> Array.ofSeq

        let awaitedSignals = awaitedDependencies |> Array.map (fun entry -> entry :> ISignal)
        hub.Subscribe awaitedSignals (fun () ->

            // children can ask for a build
            let childrenRebuild, childrenLastBuild =
                awaitedDependencies |> Seq.fold (fun (childrenRebuild, childrenLastBuild) childComputed ->
                    let childRebuild, childLastBuild = childComputed.Value
                    let nodeRebuild = childrenRebuild || childRebuild
                    let nodeLastBuild = max childrenLastBuild childLastBuild
                    nodeRebuild, nodeLastBuild) (false, DateTime.MinValue)

            let summary, nodeRebuild, nodeLastBuild =
                if node.Forced then
                    Log.Debug("{nodeId} must rebuild because node is forced", nodeId)
                    None, true, DateTime.MaxValue

                elif childrenRebuild then
                    Log.Debug("{nodeId} must be rebuild because children must rebuild", nodeId)
                    None, true, DateTime.MaxValue

                else
                    let useRemoteCache = Cacheability.Never <> (node.Cache &&& cacheMode)
                    let cacheEntryId = $"{node.ProjectHash}/{node.Target}/{node.Hash}"

                    // check first if it's possible to restore previously built state
                    let summary =
                        if node.Cache = Cacheability.Never then
                            Log.Debug("{nodeId} must rebuild because node never cached", nodeId)
                            None
                        else
                            // get task execution summary & take care of retrying failed tasks
                            match cache.TryGetSummary useRemoteCache cacheEntryId with
                            | Some summary when summary.Status = Cache.TaskStatus.Failure && options.Retry ->
                                Log.Debug("{nodeId} must rebuild because node is failed and retry requested", nodeId)
                                None
                            | Some summary ->
                                Log.Debug("{nodeId} has existing build summary", nodeId)
                                Some summary
                            | _ ->
                                Log.Debug("{nodeId} has no build summary", nodeId)
                                None

                    match summary with
                    | Some summary ->
                        if summary.StartedAt < childrenLastBuild then
                            Log.Debug("{nodeId} must rebuild because it is older than one of child", nodeId)
                            cache.Invalidate cacheEntryId
                            None, true, DateTime.MaxValue
                        else
                            (Some summary), false, summary.EndedAt
                    | _ ->
                        None, true, DateTime.MaxValue

            let node = { node with BuildSummary = summary
                                   Required = summary |> Option.isNone }
            allNodes.TryAdd(nodeId, node) |> ignore

            nodeComputed.Value <- (nodeRebuild, nodeLastBuild)
        )

    let status = hub.WaitCompletion()

    let endedAt = DateTime.UtcNow

    let trimDuration = endedAt - startedAt
    Log.Debug("Consistency: {duration}", trimDuration)
    { graph with Nodes = allNodes |> Map.ofDict }





let markRequired (graph: Workspace) (options: Configuration.Options) =
    let startedAt = DateTime.UtcNow

    let reversedDependencies =
        let reversedEdges =
            graph.Nodes
            |> Seq.collect (fun (KeyValue(nodeId, node)) -> node.Dependencies |> Seq.map (fun dependency -> dependency, nodeId))
            |> Seq.groupBy fst
            |> Seq.map (fun (k, v) -> k, v |> Seq.map snd |> List.ofSeq)
            |> Map.ofSeq

        graph.Nodes
        |> Map.map (fun _ _ -> [])
        |> Map.addMap reversedEdges

    let allNodes = ConcurrentDictionary<string, Node>()
    let hubOutputs = Hub.Create(options.MaxConcurrency)
    for (KeyValue(depNodeId, nodeIds)) in reversedDependencies do
        let nodeComputed = hubOutputs.CreateComputed<bool> depNodeId

        // await dependencies
        let awaitedDependencies =
            nodeIds
            |> Seq.map (fun awaitedProjectId -> hubOutputs.GetComputed<bool> awaitedProjectId)
            |> Array.ofSeq

        let awaitedSignals = awaitedDependencies |> Array.map (fun entry -> entry :> ISignal)
        hubOutputs.Subscribe awaitedSignals (fun () ->
            let node = graph.Nodes[depNodeId]
            let childRequired =
                awaitedDependencies |> Seq.fold (fun parentRequired dep -> parentRequired || dep.Value) (node.BuildSummary |> Option.isNone)
            let node = { node with Required = node.Forced || node.Required || childRequired }
            allNodes.TryAdd(depNodeId, node) |> ignore
            nodeComputed.Value <- childRequired)

    let status = hubOutputs.WaitCompletion()
    match status with
    | Status.Ok -> ()
    | Status.SubcriptionNotRaised projectId -> TerrabuildException.Raise($"Node {projectId} is unknown")
    | Status.SubscriptionError exn -> TerrabuildException.Raise("Optimization error", exn)

    let endedAt = DateTime.UtcNow

    let requiredDuration = endedAt - startedAt

    Log.Debug("Required: {duration}", requiredDuration)
    { graph with Nodes = allNodes |> Map.ofDict }


