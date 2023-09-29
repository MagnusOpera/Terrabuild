module Graph
open System.Collections.Concurrent
open Collections

type Node = {
    ProjectId: string
    TargetId: string
    Configuration: Configuration.ProjectConfig
    Dependencies: string set
    IsLeaf: bool
}

type WorkspaceGraph = {
    Target: string
    Nodes: Map<string, Node>
    RootNodes: Map<string, string>
}


let buildGraph (wsConfig: Configuration.WorkspaceConfig) (target: string) =
    let processedNodes = ConcurrentDictionary<string, bool>()
    let allNodes = ConcurrentDictionary<string, Node>()

    let rec buildTarget target projectId  =
        let nodeId = $"{projectId}-{target}"
        let projectConfig = wsConfig.Projects[projectId]

        // process only if a named step list exist
        match projectConfig.Steps |> Map.tryFind target with
        | Some _ ->
            if processedNodes.TryAdd(nodeId, true) then
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
                            | String.Regex "^\^(.+)$" [ parentDependsOn ] ->
                                projectConfig.Dependencies |> Seq.choose (buildTarget parentDependsOn)
                            | _ ->
                                hasInternalDependencies <- true
                                [ buildTarget dependsOn projectId ] |> Seq.choose id
                        children <- children + (childDependency |> Set.ofSeq)
                    children, hasInternalDependencies

                // NOTE: a node is considered a leaf (within this project only) if the target has no internal dependencies detected
                let isLeaf = hasInternalDependencies |> not

                let node = { ProjectId = projectId
                             TargetId = target
                             Configuration = projectConfig
                             Dependencies = children
                             IsLeaf = isLeaf }
                if allNodes.TryAdd(nodeId, node) |> not then
                    failwith "Unexpected graph building race"
            Some nodeId

        | _ ->
            None

    let rootNodes =
        wsConfig.Build.Dependencies
        |> Seq.choose (fun dependency -> buildTarget target dependency |> Option.map (fun r -> dependency, r))
        |> Map.ofSeq

    { Target = target
      Nodes = Map.ofDict allNodes 
      RootNodes = rootNodes }
