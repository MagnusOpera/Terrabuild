module Graph
open System.Collections.Concurrent
open Collections
open Terrabuild.Extensibility

type Paths = string set

[<RequireQualifiedAccess>]
type CommandLine = {
    Container: string option
    Command: string
    Arguments: string
}

type Node = {
    Project: string
    Target: string
    Dependencies: string set
    IsLeaf: bool
    Hash: string
    ProjectHash: string
    Variables: Map<string, string>
    CommandLines: CommandLine list
    Outputs: Configuration.Items
    Cache: Cacheability
}

type WorkspaceGraph = {
    Targets: string set
    Nodes: Map<string, Node>
    RootNodes: string set
}


let buildGraph (wsConfig: Configuration.WorkspaceConfig) targets =
    let processedNodes = ConcurrentDictionary<string, bool>()
    let allNodes = ConcurrentDictionary<string, Node>()

    let rec buildTarget target project =
        let nodeId = $"{project}:{target}"

        let processNode () =
            let projectConfig = wsConfig.Projects[project]

            // merge targets requirements
            let buildDependsOn =
                wsConfig.Build.Targets
                |> Map.tryFind target
                |> Option.defaultValue Set.empty
            let projDependsOn = projectConfig.Targets
                                |> Map.tryFind target
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
            match projectConfig.Steps |> Map.tryFind target with
            | Some step ->
                let hashContent = [
                    yield! step.Variables |> Seq.map (fun kvp -> $"{kvp.Key} = {kvp.Value}")
                    yield! step.CommandLines |> Seq.map (fun cmd -> $"{cmd.Container} {cmd.Command} {cmd.Arguments}")
                    yield! children |> Seq.map (fun nodeId -> allNodes[nodeId].Hash)
                    yield projectConfig.Hash
                ]

                let hash = hashContent |> String.sha256list

                // compute cacheability of this node
                let childrenCache =
                    children
                    |> Seq.fold (fun acc nodeId -> acc &&& allNodes[nodeId].Cache) Cacheability.Always

                let cache =
                    step.CommandLines
                    |> Seq.fold (fun acc cmd -> acc &&& cmd.Cache) childrenCache

                let commandLines =
                    step.CommandLines
                    |> List.map (fun cmd -> { CommandLine.Container = cmd.Container
                                              CommandLine.Command = cmd.Command
                                              CommandLine.Arguments = cmd.Arguments })

                let node = { Project = project
                             Target = target
                             Variables = step.Variables
                             CommandLines = commandLines
                             Outputs = projectConfig.Outputs
                             Dependencies = children
                             IsLeaf = isLeaf
                             Hash = hash
                             ProjectHash = projectConfig.Hash
                             Cache = cache }
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

    { Targets = targets
      Nodes = allNodes |> Map.ofDict
      RootNodes = rootNodes }
