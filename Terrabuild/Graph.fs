module Graph
open System.Collections.Concurrent
open Collections

type Node = {
    ProjectId: string
    TargetId: string
    Configuration: Configuration.ProjectConfig
    Dependencies: string set
    Files: string list
    FilesHash: string
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
                let children =
                    dependsOns
                    |> Seq.collect (fun dependsOn -> 
                        if dependsOn.StartsWith("^") then
                            let dependsOn = dependsOn.Substring(1)
                            projectConfig.Dependencies |> Seq.choose (buildTarget dependsOn)
                        else
                            [ buildTarget dependsOn projectId ] |> Seq.choose id)
                    |> Set.ofSeq

                let isLeaf = dependsOns |> Seq.exists (fun dependsOn -> dependsOn.StartsWith("^"))

                let projectDir = IO.combine wsConfig.Directory projectId
                let ignoreFiles =
                    projectConfig.Outputs 
                    |> Seq.map (fun output -> IO.combine projectDir output)
                    |> Set.ofSeq
                
                let files = projectDir |> IO.enumerateFilesBut ignoreFiles |> List.ofSeq
                let hash = files |> Hash.computeFilesSha

                let files = files |> List.map (IO.relativePath wsConfig.Directory)

                let node = { ProjectId = projectId
                             TargetId = target
                             Configuration = projectConfig
                             Files = files
                             FilesHash = hash
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
