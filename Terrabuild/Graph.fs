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


// NOTE: can be easily parallelized using ConcurrentHashSet and ConcurrentDictionary
let buildGraph (wsConfig: WorkspaceConfig) (target: string) =
    let processedNodes = HashSet<string>()
    let allNodes = Dictionary<string, Node>()
    let rootNodes = HashSet<string>()

    let rec buildTarget projectId target (caller: HashSet<string>) =
        let nodeId = $"{projectId}-{target}"
        let projectConfig = wsConfig.Projects[projectId]
        match projectConfig.Targets |> Map.tryFind target with
        | Some projectTarget -> if processedNodes.Contains(nodeId) |> not then
                                    processedNodes.Add(nodeId) |> ignore
                                    caller.Add(nodeId) |> ignore

                                    // merge targets rquirements
                                    let buildDependsOn = 
                                        wsConfig.Build.Targets
                                        |> Map.tryFind target
                                        |> Option.map (fun x -> x.DependsOn |> Set.ofList)
                                        |> Option.defaultValue Set.empty
                                    let projDependsOn = projectTarget.DependsOn |> Set.ofSeq
                                    let dependsOns = buildDependsOn + projDependsOn

                                    // apply on each dependency
                                    let children = HashSet<string>()
                                    for dependsOn in dependsOns do
                                        if dependsOn.StartsWith("^") then
                                            let dependsOn = dependsOn.Substring(1)
                                            for dependency in projectConfig.Dependencies do
                                                buildTarget dependency dependsOn children
                                        else
                                            buildTarget projectId dependsOn children

                                    let projectDir = IO.combine wsConfig.Directory projectId
                                    let listing =
                                        match Exec.execCaptureOutput projectDir "git" "ls-tree -rl HEAD ." with
                                        | Exec.Success (listing, _) -> listing
                                        | _ -> failwith "Failed to get listing"

                                    let node = { ProjectId = projectId
                                                 TargetId = target
                                                 Configuration = projectConfig
                                                 Listing = listing
                                                 Dependencies = children |> Set.ofSeq }
                                    allNodes.Add(nodeId, node)
        | _ -> ()

    for dependency in wsConfig.Build.Dependencies do
        buildTarget dependency target rootNodes

    { Nodes = Map.ofDict allNodes 
      RootNodes = Set.ofSeq rootNodes }

