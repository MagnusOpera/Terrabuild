module Graph
open System.Collections.Concurrent
open Collections

type Paths = string set

[<RequireQualifiedAccess>]
type Action = {
    Variables: Map<string, string>
    CommandLines: Configuration.ContaineredCommandLine list
}

type Node = {
    Project: string
    Target: string
    Dependencies: string set
    IsLeaf: bool
    Hash: string
    Action: Action
    Outputs: Configuration.Paths
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
        let nodeId = $"{project}-{target}"

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
                ]

                let hash = hashContent |> String.sha256list

                let node = { Project = project
                             Target = target
                             Action = { Action.Variables = step.Variables
                                        Action.CommandLines = step.CommandLines }
                             Outputs = projectConfig.Outputs
                             Dependencies = children
                             IsLeaf = isLeaf
                             Hash = hash }
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
