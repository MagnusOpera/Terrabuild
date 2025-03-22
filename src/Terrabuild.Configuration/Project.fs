module Terrabuild.Configuration.Project
open Terrabuild.Expressions
open HCL
open Errors


[<RequireQualifiedAccess>]
type ProjectBlock =
    { Init: string option
      Dependencies: Expr
      Links: Expr
      Outputs: Expr
      Ignores: Expr
      Includes: Expr
      Labels: Expr }


type Step =
    { Extension: string
      Command: string
      Parameters: Map<string, Expr> }

[<RequireQualifiedAccess>]
type TargetBlock =
    { Rebuild: Expr
      Outputs: Expr
      DependsOn: Expr
      Cache: Expr
      Steps: Step list }

[<RequireQualifiedAccess>]
type LocalsBlock =
    { Locals: Map<string, Expr> }

[<RequireQualifiedAccess>]
type ProjectFile =
    { Project: ProjectBlock
      Extensions: Map<string, ExtensionBlock>
      Targets: Map<string, TargetBlock>
      Locals: Map<string, Expr> }




let private map (blocks: Block list) =
    let rec buildProject (blocks: Block list)
                         (project: ProjectBlock option)
                         (extensions: Map<string, ExtensionBlock>)
                         (targets: Map<string, TargetBlock>)
                         (locals: LocalsBlock option) =
        match blocks with
        | [] ->
            let project =
                match project with
                | None -> raiseParseError "project not declared"
                | Some workspace -> workspace

            let locals =
                match locals with
                | None -> Map.empty
                | Some locals -> locals.Locals

            { ProjectFile.Project = project
              ProjectFile.Extensions = extensions
              ProjectFile.Targets = targets
              ProjectFile.Locals = locals }

        | block::blocks ->
            match block.Resource, block.Name with
            // =============================================================================================
            | "project", init ->
                if project <> None then raiseParseError "multiple project declared"

                block
                |> checkAllowedAttributes ["dependencies"; "links"; "outputs"; "ignores"; "includes"; "labels"]
                |> checkNoNestedBlocks
                |> ignore

                let dependencies = block.Attributes |> tryFindAttribute "dependencies" |> valueOrDefault Expr.EmptyList
                let links = block.Attributes |> tryFindAttribute "links" |> valueOrDefault Expr.EmptyList
                let outputs = block.Attributes |> tryFindAttribute "outputs" |> valueOrDefault Expr.EmptyList
                let ignores = block.Attributes |> tryFindAttribute "ignores" |> valueOrDefault Expr.EmptyList
                let includes = block.Attributes |> tryFindAttribute "includes" |> valueOrDefault Expr.EmptyList
                let labels = block.Attributes |> tryFindAttribute "labels" |> valueOrDefault Expr.EmptyList

                let project = { ProjectBlock.Init = init
                                ProjectBlock.Dependencies = dependencies
                                ProjectBlock.Links = links
                                ProjectBlock.Outputs = outputs
                                ProjectBlock.Ignores = ignores
                                ProjectBlock.Includes = includes
                                ProjectBlock.Labels = labels }

                buildProject blocks (Some project) extensions targets locals

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

                buildProject blocks project (Map.add name extension extensions) targets locals


            // =============================================================================================
            | "target", Some name ->
                block
                |> checkAllowedAttributes ["rebuild"; "outputs"; "dependsOn"; "cache"]
                |> checkAllowedNestedBlocks ["step"]
                |> ignore

                if targets.ContainsKey name then raiseParseError $"Duplicate target: {name}"

                let rebuild = block.Attributes |> tryFindAttribute "rebuild" |> valueOrDefault Expr.False
                let outputs = block.Attributes |> tryFindAttribute "outputs" |> valueOrDefault Expr.EmptyList
                let dependsOn = block.Attributes |> tryFindAttribute "dependsOn" |> valueOrDefault Expr.EmptyList
                let cache = block.Attributes |> tryFindAttribute "cache" |> valueOrDefault Expr.EmptyList
                let steps =
                    block.Blocks
                    |> List.map (fun step ->
                        step
                        |> checkNoNestedBlocks
                        |> ignore

                        let command =
                            match step.Name with
                            | Some name -> name
                            | _ ->raiseParseError "command is not declared"

                        let parameters =
                            step.Attributes
                            |> List.map (fun a -> (a.Name, a.Value))
                            |> Map.ofList

                        { Extension = step.Resource
                          Command = command
                          Parameters = parameters })

                let target = { TargetBlock.Rebuild = rebuild
                               TargetBlock.Outputs = outputs
                               TargetBlock.DependsOn = dependsOn
                               TargetBlock.Cache = cache
                               TargetBlock.Steps = steps }

                buildProject blocks project extensions (Map.add name target targets) locals

            // =============================================================================================
            | "locals", None ->
                block
                |> checkNoNestedBlocks
                |> ignore

                let variables = block.Attributes
                                |> List.map (fun a -> (a.Name, a.Value))
                                |> Map.ofList

                let locals = { LocalsBlock.Locals = variables }
                buildProject blocks project extensions targets (Some locals)

            // =============================================================================================
            | resource, _ -> raiseParseError $"unexpected block: {resource}"

    buildProject blocks None Map.empty Map.empty None


let parse txt =
    let hcl = FrontEnd.HCL.parse txt
    map hcl.Blocks
