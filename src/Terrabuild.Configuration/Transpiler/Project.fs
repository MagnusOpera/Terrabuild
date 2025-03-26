module Transpiler.Project
open AST.HCL
open AST.Common
open AST.Project
open Errors
open Terrabuild.Expressions
open Common


type ProjectBuilder =
    { Project: ProjectBlock option
      Extensions: Map<string, ExtensionBlock>
      Targets: Map<string, TargetBlock>
      Locals: LocalsBlock option }

let transpile (blocks: Block list) =
    let rec buildProject (blocks: Block list) (builder: ProjectBuilder) =
        match blocks with
        | [] ->
            let project =
                match builder.Project with
                | None -> { ProjectBlock.Init = None
                            ProjectBlock.Dependencies = None
                            ProjectBlock.Links = None
                            ProjectBlock.Outputs = None
                            ProjectBlock.Ignores = None
                            ProjectBlock.Includes = None
                            ProjectBlock.Labels = Set.empty }
                | Some workspace -> workspace

            let locals =
                match builder.Locals with
                | None -> Map.empty
                | Some locals -> locals.Locals

            { ProjectFile.Project = project
              ProjectFile.Extensions = builder.Extensions
              ProjectFile.Targets = builder.Targets
              ProjectFile.Locals = locals }

        | block::blocks ->
            match block.Resource, block.Name with
            // =============================================================================================
            | "project", init ->
                if builder.Project <> None then raiseParseError "multiple project declared"

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

                buildProject blocks { builder with Project = Some project }

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

                buildProject blocks { builder with Extensions = builder.Extensions |> Map.add name extension }


            // =============================================================================================
            | "target", Some name ->
                block
                |> checkAllowedAttributes ["rebuild"; "outputs"; "depends_on"; "cache"]
                |> ignore

                if builder.Targets.ContainsKey name then raiseParseError $"Duplicate target: {name}"

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

                buildProject blocks { builder with Targets = builder.Targets |> Map.add name target }

            // =============================================================================================
            | "locals", None ->
                block
                |> checkNoNestedBlocks
                |> ignore

                let variables = block.Attributes
                                |> List.map (fun a -> (a.Name, a.Value))
                                |> Map.ofList

                let locals = { LocalsBlock.Locals = variables }
                buildProject blocks { builder with Locals = Some locals }

            // =============================================================================================
            | resource, _ -> raiseParseError $"unexpected block: {resource}"

    let builder =
        { Project = None
          Extensions = Map.empty
          Targets = Map.empty 
          Locals = None }
    buildProject blocks builder

