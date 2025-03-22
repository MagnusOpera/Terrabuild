module Terrabuild.Configuration.Workspace
open Errors
open Terrabuild.Expressions
open HCL



[<RequireQualifiedAccess>]
type WorkspaceBlock =
    { Id: Expr
      Ignores: Expr }

[<RequireQualifiedAccess>]
type TargetBlock =
    { DependsOn: Expr
      Rebuild: Expr }

[<RequireQualifiedAccess>]
type ConfigurationBlock =
    { Variables: Map<string, Expr> }

[<RequireQualifiedAccess>]
type WorkspaceFile =
    { Workspace: WorkspaceBlock
      Targets: Map<string, TargetBlock>
      Configurations: Map<string, ConfigurationBlock>
      Extensions: Map<string, ExtensionBlock> }



let private map (blocks: Block list) =
    let rec buildWorkspace (blocks: Block list)
                           (workspace: WorkspaceBlock option)
                           (targets: Map<string, TargetBlock>)
                           (configurations: Map<string, ConfigurationBlock>)
                           (extensions: Map<string, ExtensionBlock>) =
        match blocks with
        | [] ->
            let workspace =
                match workspace with
                | None -> raiseParseError "workspace not declared"
                | Some workspace -> workspace

            { WorkspaceFile.Workspace = workspace
              WorkspaceFile.Targets = targets
              WorkspaceFile.Configurations = configurations
              WorkspaceFile.Extensions = extensions }

        | block::blocks ->
            match block.Resource, block.Name with
            // =============================================================================================
            | "workspace", None ->
                if workspace <> None then raiseParseError "multiple workspace declared"

                block
                |> checkAllowedAttributes ["id"; "ignores"]
                |> checkNoNestedBlocks
                |> ignore

                let id = block.Attributes  |> tryFindAttribute "id" |> valueOrDefault Expr.Nothing
                let ignores = block.Attributes |> tryFindAttribute "ignores" |> valueOrDefault Expr.EmptyMap
                let workspace = { WorkspaceBlock.Id = id
                                  WorkspaceBlock.Ignores = ignores }
                buildWorkspace blocks (Some workspace) targets configurations extensions

            // =============================================================================================
            | "target", Some name ->
                block
                |> checkAllowedAttributes ["dependsOn"; "rebuild"]
                |> checkNoNestedBlocks
                |> ignore

                let dependsOn = block.Attributes |> tryFindAttribute "dependsOn" |> valueOrDefault Expr.EmptyList
                let rebuild = block.Attributes |> tryFindAttribute "rebuild" |> valueOrDefault Expr.False
                if targets.ContainsKey name then raiseParseError $"Duplicate target: {name}"

                let target = { TargetBlock.DependsOn = dependsOn
                               TargetBlock.Rebuild = rebuild }
                buildWorkspace blocks workspace (Map.add name target targets) configurations extensions

            // =============================================================================================
            | "configuration", Some name ->
                block
                |> checkNoNestedBlocks
                |> ignore

                let variables = block.Attributes
                                |> List.map (fun a -> (a.Name, a.Value))
                                |> Map.ofList
                let configuration = { ConfigurationBlock.Variables = variables }
                buildWorkspace blocks workspace targets (Map.add name configuration configurations) extensions

            // =============================================================================================
            | "extension", Some name ->
                block
                |> checkAllowedAttributes ["container"; "platform"; "variables"; "script"; "defaults"]
                |> checkAllowedNestedBlocks ["defaults"]
                |> ignore

                let container = block.Attributes |> tryFindAttribute "container" |> valueOrDefault Expr.Nothing
                let platform = block.Attributes |> tryFindAttribute "platform" |> valueOrDefault Expr.Nothing
                let variables = block.Attributes |> tryFindAttribute "variables" |> valueOrDefault Expr.EmptyList
                let script = block.Attributes |> tryFindAttribute "script" |> valueOrDefault Expr.Nothing
                let defaults =
                    match block |> tryFindBlock "defaults" with
                    | Some defaults ->
                        defaults
                        |> checkNoNestedBlocks
                        |> ignore

                        defaults.Attributes
                        |> List.map (fun a -> (a.Name, a.Value))
                        |> Map.ofList
                    | None -> Map.empty

                let extension = { ExtensionBlock.Container = container
                                  ExtensionBlock.Platform = platform
                                  ExtensionBlock.Variables = variables
                                  ExtensionBlock.Script = script
                                  ExtensionBlock.Defaults = defaults } 
                buildWorkspace blocks workspace targets configurations (Map.add name extension extensions)

            // =============================================================================================
            | resource, _ -> raiseParseError $"unexpected block: {resource}"

    buildWorkspace blocks None Map.empty Map.empty Map.empty



let parse txt =
    let hcl = FrontEnd.HCL.parse txt
    map hcl.Blocks
