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


let toWorkspace (block: Block) =
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

    { WorkspaceBlock.Id = id
      WorkspaceBlock.Ignores = ignores }


let toTarget (block: Block) =
    block
    |> checkAllowedAttributes ["depends_on"; "rebuild"]
    |> checkNoNestedBlocks
    |> ignore

    let dependsOn =
        block |> tryFindAttribute "depends_on" 
        |> Option.bind (Eval.asStringSetOption << simpleEval)
        |> Option.defaultValue Set.empty
    let rebuild = block |> tryFindAttribute "rebuild"

    { TargetBlock.DependsOn = dependsOn
      TargetBlock.Rebuild = rebuild }

    
let toConfiguration (block: Block) =
    block
    |> checkNoNestedBlocks
    |> ignore

    let variables = block.Attributes
                    |> List.map (fun a -> (a.Name, a.Value))
                    |> Map.ofList
    
    { ConfigurationBlock.Variables = variables }


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
            | Workspace ->
                if builder.Workspace <> None then raiseParseError "multiple workspace declared"
                let workspace = toWorkspace block
                buildWorkspace blocks { builder with Workspace = Some workspace }

            | Target name ->
                if builder.Targets.ContainsKey name then raiseParseError $"Duplicate target: {name}"
                let target = toTarget block
                buildWorkspace blocks { builder with Targets = builder.Targets |> Map.add name target }

            | Configuration name ->
                if builder.Configurations.ContainsKey name then raiseParseError $"Duplicate configuration: {name}"
                let configuration = toConfiguration block
                buildWorkspace blocks { builder with Configurations = builder.Configurations |> Map.add name configuration }

            | Extension name ->
                if builder.Extensions.ContainsKey name then raiseParseError $"Duplicate extension: {name}"
                let extension = toExtension block
                buildWorkspace blocks { builder with Extensions = builder.Extensions |> Map.add name extension }

            | UnknownBlock -> raiseParseError $"unexpected block: {block.Resource}"

    let builder =
        { Workspace = None
          Targets = Map.empty
          Configurations = Map.empty
          Extensions = Map.empty }
    buildWorkspace blocks builder

