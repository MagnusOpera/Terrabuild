module Transpiler.Workspace
open Errors
open Terrabuild.Expressions
open Common
open AST.HCL
open AST.Common
open AST.Workspace

type WorkspaceBuilder =
    { Workspace: WorkspaceBlock option
      Targets: Map<string, TargetBlock>
      Configurations: Map<string, ConfigurationBlock>
      Extensions: Map<string, ExtensionBlock> }


let (|Workspace|Target|Configuration|Extension|UnknownBlock|) (block: Block) =
    match block.Resource, block.Name with
    | "workspace", None -> Workspace
    | "target", Some name -> Target name
    | "configuration", Some name -> Configuration name
    | "extension", Some name -> Extension name
    | _ -> UnknownBlock


let transpile (blocks: Block list) =
    let rec buildWorkspace (blocks: Block list) (builder: WorkspaceBuilder) =
        match blocks with
        | [] ->
            let workspace =
                match builder.Workspace with
                | None -> { WorkspaceBlock.Id = None
                            WorkspaceBlock.Ignores = None }
                | Some workspace -> workspace

            { WorkspaceFile.Workspace = workspace
              WorkspaceFile.Targets = builder.Targets
              WorkspaceFile.Configurations = builder.Configurations
              WorkspaceFile.Extensions = builder.Extensions }

        | block::blocks ->
            match block with
            // =============================================================================================
            | Workspace ->
                if builder.Workspace <> None then raiseParseError "multiple workspace declared"

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
                buildWorkspace blocks { builder with Workspace = Some workspace }

            // =============================================================================================
            | Target name ->
                block
                |> checkAllowedAttributes ["depends_on"; "rebuild"]
                |> checkNoNestedBlocks
                |> ignore

                let dependsOn =
                    block |> tryFindAttribute "depends_on" 
                    |> Option.bind (Eval.asStringSetOption << simpleEval)
                    |> Option.defaultValue Set.empty
                let rebuild = block |> tryFindAttribute "rebuild"
                if builder.Targets.ContainsKey name then raiseParseError $"Duplicate target: {name}"

                let target = { TargetBlock.DependsOn = dependsOn
                               TargetBlock.Rebuild = rebuild }
                buildWorkspace blocks { builder with Targets = builder.Targets |> Map.add name target }

            // =============================================================================================
            | Configuration name ->
                block
                |> checkNoNestedBlocks
                |> ignore

                let variables = block.Attributes
                                |> List.map (fun a -> (a.Name, a.Value))
                                |> Map.ofList
                let configuration = { ConfigurationBlock.Variables = variables }
                buildWorkspace blocks { builder with Configurations = builder.Configurations |> Map.add name configuration }

            // =============================================================================================
            | Extension name ->
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
                buildWorkspace blocks { builder with Extensions = builder.Extensions |> Map.add name extension }

            // =============================================================================================
            | _ -> raiseParseError $"unexpected block: {block.Resource}"

    let builder =
        { Workspace = None
          Targets = Map.empty
          Configurations = Map.empty
          Extensions = Map.empty }
    buildWorkspace blocks builder

