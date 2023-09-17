module Graph
open System.Collections.Concurrent
open System.Collections.Generic
open Helpers.Collections
open Configuration
open Helpers


type Node = {
    ProjectId: string
    TargetId: string
    Configuration: ProjectConfig
    Dependencies: Set<string>
    TreeFiles: string
    Changes: string
}

type WorkspaceGraph = {
    Target: string
    Nodes: Map<string, Node>
    RootNodes: Map<string, string>
}


let buildGraph (wsConfig: WorkspaceConfig) (target: string) =
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
                    |> Option.defaultValue List.empty
                    |> Set.ofList
                let projDependsOn = projectConfig.Targets
                                    |> Map.tryFind target
                                    |> Option.defaultValue List.empty
                                    |> Set.ofList
                let dependsOns = buildDependsOn + projDependsOn

                // apply on each dependency
                let children =
                    dependsOns
                    |> Seq.collect (fun dependsOn -> 
                        if dependsOn.StartsWith("^") then
                            let dependsOn = dependsOn.Substring(1)
                            projectConfig.Dependencies |> List.choose (buildTarget dependsOn)
                        else
                            [ buildTarget dependsOn projectId ] |> List.choose id)
                    |> Set.ofSeq

                let projectDir = IO.combine wsConfig.Directory projectId
                let workingFiles = Git.listFiles projectDir
                let changes = Git.listChanges projectDir
                let node = { ProjectId = projectId
                             TargetId = target
                             Configuration = projectConfig
                             TreeFiles = workingFiles
                             Changes = changes
                             Dependencies = children }
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
