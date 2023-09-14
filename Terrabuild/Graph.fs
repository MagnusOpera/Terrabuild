module Graph
open System.Collections.Generic
open Helpers.Collections
open Configuration
open Helpers


type Node = {
    ProjectId: string
    TargetId: string
    Configuration: ProjectConfig
    Dependencies: Set<string>
    Listing: string
}

type WorkspaceGraph = {
    Nodes: Map<string, Node>
    RootNodes: Set<string>
}


let buildGraph (wsConfig: WorkspaceConfig) (target: string) =
    let processedNodes = HashSet<string>()
    let allNodes = Dictionary<string, Node>()

    let rec buildTarget target projectId  =
        let nodeId = $"{projectId}-{target}"
        let projectConfig = wsConfig.Projects[projectId]
        match projectConfig.Targets |> Map.tryFind target with
        | Some projectTarget -> 
            if processedNodes.Contains(nodeId) |> not then
                processedNodes.Add(nodeId) |> ignore

                // merge targets rquirements
                let buildDependsOn = 
                    wsConfig.Build.Targets
                    |> Map.tryFind target
                    |> Option.map (fun x -> x.DependsOn |> Set.ofList)
                    |> Option.defaultValue Set.empty
                let projDependsOn = projectTarget.DependsOn |> Set.ofSeq
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
                let listing = Git.listFiles projectDir
                let node = { ProjectId = projectId
                             TargetId = target
                             Configuration = projectConfig
                             Listing = listing
                             Dependencies = children }
                allNodes.Add(nodeId, node)
            Some nodeId

        | _ ->
            None

    let rootNodes =
        wsConfig.Build.Dependencies
        |> Seq.choose (buildTarget target)
        |> Set.ofSeq

    { Nodes = Map.ofDict allNodes 
      RootNodes = rootNodes }
