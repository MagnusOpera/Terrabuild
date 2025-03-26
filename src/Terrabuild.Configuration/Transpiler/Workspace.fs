module Transpiler.Workspace
open Errors
open Terrabuild.Expressions
open Common
open AST.HCL
open AST.Common
open AST.Workspace

let transpile (blocks: Block list) =
    let rec buildWorkspace (blocks: Block list)
                           (workspace: WorkspaceBlock option)
                           (targets: Map<string, TargetBlock>)
                           (configurations: Map<string, ConfigurationBlock>)
                           (extensions: Map<string, ExtensionBlock>) =
        match blocks with
        | [] ->
            let workspace =
                match workspace with
                | None -> { WorkspaceBlock.Id = None
                            WorkspaceBlock.Ignores = None }
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

                let id =
                    block |> tryFindAttribute "id"
                    |> Option.bind (Eval.asStringOption << simpleEval)
                let ignores =
                    block |> tryFindAttribute "ignores"
                    |> Option.bind (Eval.asStringSetOption << simpleEval)
                let workspace = { WorkspaceBlock.Id = id
                                  WorkspaceBlock.Ignores = ignores }
                buildWorkspace blocks (Some workspace) targets configurations extensions

            // =============================================================================================
            | "target", Some name ->
                block
                |> checkAllowedAttributes ["depends_on"; "rebuild"]
                |> checkNoNestedBlocks
                |> ignore

                let dependsOn =
                    block |> tryFindAttribute "depends_on" 
                    |> Option.bind (Eval.asStringSetOption << simpleEval)
                    |> Option.defaultValue Set.empty
                let rebuild = block |> tryFindAttribute "rebuild"
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

                let container = block |> tryFindAttribute "container"
                let platform = block |> tryFindAttribute "platform"
                let variables = block |> tryFindAttribute "variables"
                let script = block |> tryFindAttribute "script"
                let defaults =
                    block
                    |> tryFindBlock "defaults"
                    |> Option.map (fun defaults ->
                        defaults
                        |> checkNoNestedBlocks
                        |> ignore

                        defaults.Attributes
                        |> List.map (fun a -> (a.Name, a.Value))
                        |> Map.ofList)

                let extension = { ExtensionBlock.Container = container
                                  ExtensionBlock.Platform = platform
                                  ExtensionBlock.Variables = variables
                                  ExtensionBlock.Script = script
                                  ExtensionBlock.Defaults = defaults } 
                buildWorkspace blocks workspace targets configurations (Map.add name extension extensions)

            // =============================================================================================
            | resource, _ -> raiseParseError $"unexpected block: {resource}"

    buildWorkspace blocks None Map.empty Map.empty Map.empty


