module Graph
open System
open System.Collections.Concurrent
open Collections
open Terrabuild.Extensibility

type Paths = string set


type Node = {
    Id: string
    Hash: string
    Project: string
    Target: string
    Dependencies: string set
    ProjectHash: string
    Outputs: string set
    Cache: Cacheability
    IsLeaf: bool

    TargetHash: string
    CommandLines: Configuration.ContaineredActionBatch list
}

type WorkspaceGraph = {
    Targets: string set
    Nodes: Map<string, Node>
    RootNodes: string set
}


let buildGraph (wsConfig: Configuration.WorkspaceConfig) (targets: string set) =
    let processedNodes = ConcurrentDictionary<string, bool>()
    let allNodes = ConcurrentDictionary<string, Node>()

    let rec buildTarget targetName project =
        let nodeId = $"{project}:{targetName}"

        let processNode () =
            let projectConfig = wsConfig.Projects[project]

            // merge targets requirements
            let buildDependsOn =
                wsConfig.Targets
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
                let mutable children = Set.empty
                let mutable hasInternalDependencies = false

                for dependsOn in dependsOns do
                    let childDependencies =
                        match dependsOn with
                        | String.Regex "^\^([a-zA-Z][_a-zA-Z0-9]+)$" [ parentDependsOn ] ->
                            projectConfig.Dependencies
                            |> Seq.collect (buildTarget parentDependsOn)
                        | _ ->
                            hasInternalDependencies <- true
                            buildTarget dependsOn project
                    children <- children + (childDependencies |> Set)
                children, hasInternalDependencies

            // NOTE: a node is considered a leaf (within this project only) if the target has no internal dependencies detected
            let isLeaf = hasInternalDependencies |> not

            // only generate computation node - that is node that generate something
            // barrier nodes are just discarded and dependencies lift level up
            match projectConfig.Targets |> Map.tryFind targetName with
            | Some target ->
                let hashContent = [
                    yield! target.Variables |> Seq.map (fun kvp -> $"{kvp.Key} = {kvp.Value}")
                    yield! target.Actions |> Seq.collect (fun batch -> 
                        batch.Actions |> Seq.map (fun cmd ->
                            $"{batch.Container} {cmd.Command} {cmd.Arguments}"))
                    yield! children |> Seq.map (fun nodeId -> allNodes[nodeId].Hash)
                    yield projectConfig.Hash
                ]

                let hash = hashContent |> Hash.sha256list

                // compute cacheability of this node
                let childrenCache =
                    children
                    |> Seq.fold (fun acc nodeId -> acc &&& allNodes[nodeId].Cache) Cacheability.Always

                let cache =
                    target.Actions
                    |> Seq.fold (fun acc cmd -> acc &&& cmd.Cache) childrenCache

                let node = { Id = nodeId
                             Hash = hash
                             Project = project
                             Target = targetName
                             CommandLines = target.Actions
                             Outputs = projectConfig.Outputs
                             Dependencies = children
                             IsLeaf = isLeaf
                             ProjectHash = projectConfig.Hash
                             Cache = cache
                             TargetHash = target.Hash }
                if allNodes.TryAdd(nodeId, node) |> not then
                    failwith "Unexpected graph building race"
                [ nodeId ]
            | _ ->
                children |> List.ofSeq

        if processedNodes.TryAdd(nodeId, true) then processNode()
        else [ nodeId ]

    let rootNodes =
        wsConfig.Dependencies |> Seq.collect (fun dependency ->
            targets |> Seq.collect (fun target -> buildTarget target dependency))
        |> Set

    $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} {allNodes.Count} tasks to evaluate" |> Terminal.writeLine

    { Targets = targets
      Nodes = allNodes |> Map.ofDict
      RootNodes = rootNodes }



let graph (graph: WorkspaceGraph) =
    let projects =
        graph.Nodes.Values
        |> Seq.groupBy (fun x -> x.Project)
        |> Map.ofSeq
        |> Map.map (fun _ v -> v |> List.ofSeq)

    let colors =
        projects
        |> Map.map (fun k v ->
            let hash = Hash.sha256 k
            $"#{hash.Substring(0, 3)}")

    let mermaid = seq {
        "flowchart LR"
        $"classDef bold stroke:black,stroke-width:3px"

        // declare colors
        for (KeyValue(project, color)) in colors do
            $"classDef {project} fill:{color}"

        // nodes and arrows
        for (KeyValue(nodeId, node)) in graph.Nodes do
            let srcNode = graph.Nodes |> Map.find nodeId
            for dependency in node.Dependencies do
                let dstNode = graph.Nodes |> Map.find dependency
                $"{srcNode.Hash}([{srcNode.Id}]) --> {dstNode.Hash}([{dstNode.Id}])"

            $"class {srcNode.Hash} {srcNode.Project}"
            if graph.RootNodes |> Set.contains srcNode.Id then $"class {srcNode.Hash} bold"
    }

    mermaid






