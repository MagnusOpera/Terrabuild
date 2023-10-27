module Graph
open System.Collections.Concurrent
open Collections

type Node = {
    Project: string
    Target: string
    Configuration: Configuration.ProjectConfig
    Dependencies: string set
    IsLeaf: bool
    IsPlaceholder: bool
}

type WorkspaceGraph = {
    Targets: string list
    Nodes: Map<string, Node>
    RootNodes: Map<string, string>
}


let buildGraph (wsConfig: Configuration.WorkspaceConfig) (targets: string list) =
    let processedNodes = ConcurrentDictionary<string, bool>()
    let allNodes = ConcurrentDictionary<string, Node>()

    let removePlaceholders dependency =
        let childNode = allNodes[dependency]
        let newChildren : string seq =
            if childNode.IsPlaceholder then
                childNode.Dependencies
            else [ dependency ]
        newChildren

    let rec buildTarget target projectId  =
        let nodeId = $"{projectId}-{target}"

        let processNode () =
            let projectConfig = wsConfig.Projects[projectId]

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
                    let childDependency =
                        match dependsOn with
                        | String.Regex "^\^([a-zA-Z][_a-zA-Z0-9]+)$" [ parentDependsOn ] ->
                            projectConfig.Dependencies
                            |> Seq.map (buildTarget parentDependsOn)
                            |> List.ofSeq
                        | _ ->
                            hasInternalDependencies <- true
                            [ buildTarget dependsOn projectId ]
                    let childrenNoPlaceholders =
                        childDependency
                        |> Seq.collect removePlaceholders
                    children <- children + (childrenNoPlaceholders |> Set)
                children, hasInternalDependencies

            // NOTE: a node is considered a leaf (within this project only) if the target has no internal dependencies detected
            let isLeaf = hasInternalDependencies |> not

            let isPlaceholder = projectConfig.Steps |> Map.containsKey target |> not
            let node = { Project = projectId
                         Target = target
                         Configuration = projectConfig
                         Dependencies = children
                         IsLeaf = isLeaf
                         IsPlaceholder = isPlaceholder }
            if allNodes.TryAdd(nodeId, node) |> not then
                failwith "Unexpected graph building race"

        if processedNodes.TryAdd(nodeId, true) then processNode()
        nodeId

    let rootNodes =
        Map [
            for dependency in wsConfig.Dependencies do
                for target in targets do
                    dependency, buildTarget target dependency
                    let nodeIds =
                        [ buildTarget target dependency ]
                        |> Seq.collect removePlaceholders
                    for nodeId in nodeIds do
                        let node = allNodes[nodeId]
                        node.Project, nodeId
        ]

    { Targets = targets
      Nodes = allNodes |> Map.ofDict |> Map.filter (fun _ node -> node.IsPlaceholder |> not)
      RootNodes = rootNodes }
