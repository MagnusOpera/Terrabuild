module FrontEnd.Project
open AST.HCL
open AST.Common
open AST.Project
open Errors
open Terrabuild.Expressions
open Common

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
                | None -> { ProjectBlock.Init = None
                            ProjectBlock.Dependencies = None
                            ProjectBlock.Links = None
                            ProjectBlock.Outputs = None
                            ProjectBlock.Ignores = None
                            ProjectBlock.Includes = None
                            ProjectBlock.Labels = Set.empty }
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

                let dependencies = block |> tryFindAttribute "dependencies"
                let links = block |> tryFindAttribute "links"
                let outputs = block |> tryFindAttribute "outputs"
                let ignores = block |> tryFindAttribute "ignores"
                let includes = block |> tryFindAttribute "includes"
                let labels =
                    block |> tryFindAttribute "labels"
                    |> Option.bind (Eval.asStringSetOption << simpleEval)
                    |> Option.defaultValue Set.empty

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

                buildProject blocks project (Map.add name extension extensions) targets locals


            // =============================================================================================
            | "target", Some name ->
                block
                |> checkAllowedAttributes ["rebuild"; "outputs"; "depends_on"; "cache"]
                |> ignore

                if targets.ContainsKey name then raiseParseError $"Duplicate target: {name}"

                let rebuild = block |> tryFindAttribute "rebuild"
                let outputs = block |> tryFindAttribute "outputs"
                let dependsOn =
                    block |> tryFindAttribute "depends_on"
                    |> Option.bind (Eval.asStringSetOption << simpleEval)
                let cache = block |> tryFindAttribute "cache"
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
    let hcl = HCL.parse txt
    map hcl.Blocks
