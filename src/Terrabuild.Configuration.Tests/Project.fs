module Terrabuild.Configuration.Tests.Project

open System.IO
open NUnit.Framework
open FsUnit

open Terrabuild.Configuration.AST
open Terrabuild.Configuration.AST.Project

open Terrabuild.Expressions

[<Test>]
let parseProject() =
    let expectedProject =
        let project =
            { ProjectBlock.Init = Some "@dotnet"
              ProjectBlock.Id = None
              ProjectBlock.DependsOn = None
              ProjectBlock.Dependencies = Expr.List [ Expr.String "../../libraries/shell-lib" ] |> Some
              ProjectBlock.Outputs = Expr.List [ Expr.String "dist" ] |> Some
              ProjectBlock.Ignores = None
              ProjectBlock.Includes = None
              ProjectBlock.Labels = Set [ "app"; "dotnet" ] }

        let extDotnet =
            { Container = None
              Platform = None
              Variables = None
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "configuration" ] |> Some }        
        let extDocker =
            { Container = None
              Platform = None
              Variables = Expr.List [ Expr.String "ARM_TENANT_ID" ] |> Some
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "configuration"
                               "image", Expr.String "ghcr.io/magnusopera/dotnet-app" ] |> Some }
        let extDummy =
            { Container = None
              Platform = None
              Variables = None
              Script = "dummy.fsx" |> Expr.String |> Some
              Defaults = None }

        let targetBuild = 
            { TargetBlock.DependsOn = Set [ "dist" ] |> Some
              TargetBlock.Rebuild = None
              TargetBlock.Outputs = None
              TargetBlock.Cache = None
              TargetBlock.Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty } ] }
        let targetDist =
            { TargetBlock.DependsOn = None
              TargetBlock.Rebuild = None
              TargetBlock.Outputs = None
              TargetBlock.Cache = None
              TargetBlock.Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty }
                                    { Extension = "@dotnet"; Command = "publish"; Parameters = Map.empty } ] }
        let targetDocker =
            { TargetBlock.DependsOn = None
              TargetBlock.Rebuild = Some (Expr.Bool false)
              TargetBlock.Outputs = None
              TargetBlock.Cache = "always" |> Expr.String |> Some
              TargetBlock.Steps = [ { Extension = "@shell"; Command = "echo"
                                      Parameters = Map [ "arguments", Expr.Function (Function.Trim,
                                                                                     [ Expr.Function (Function.Plus,
                                                                                                      [ Expr.String "building project1 "
                                                                                                        Expr.Variable "configuration" ]) ]) ] }
                                    { Extension = "@docker"; Command = "build"
                                      Parameters = Map [ "arguments", Expr.Map (Map [ "config", Expr.String "Release"
                                                                                      "my-variable", Expr.Number 42 ]) ] }
                                    { Extension = "@npm"; Command = "version"
                                      Parameters = Map [ "arguments", Expr.Variable "npm_version"
                                                         "version", Expr.String "1.0.0" ] } ] }

        { ProjectFile.Extensions = Map [ "@dotnet", extDotnet
                                         "@docker", extDocker
                                         "dummy", extDummy ]
          ProjectFile.Project = project
          ProjectFile.Targets = Map [ "build", targetBuild
                                      "dist", targetDist
                                      "docker", targetDocker ]
          ProjectFile.Locals = Map.empty }

    let content = File.ReadAllText("TestFiles/PROJECT")
    let project = Terrabuild.Configuration.FrontEnd.Project.parse content

    project
    |> should equal expectedProject
    
[<Test>]
let parseProject2() =
    let expectedProject =
        let project =
            { ProjectBlock.Init = Some "@dotnet"
              ProjectBlock.Id = None
              ProjectBlock.DependsOn = None
              ProjectBlock.Dependencies = None
              ProjectBlock.Outputs = None
              ProjectBlock.Ignores = None
              ProjectBlock.Includes = None
              ProjectBlock.Labels = Set.empty }

        let extDotnet =
            { Container = None
              Platform = None
              Variables = None
              Script = None
              Defaults = None }        

        let buildTarget = 
            { TargetBlock.Rebuild = Expr.Bool true |> Some
              TargetBlock.Outputs = Expr.List [ Expr.Function (Function.Format,
                                                               [ Expr.String "{0}{1}"
                                                                 Expr.Variable "wildcard"
                                                                 Expr.String ".dll" ])] |> Some
              TargetBlock.DependsOn = None
              TargetBlock.Cache = None
              TargetBlock.Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty } ] }

        let locals = 
            Map [ "app_name", Expr.Function (Function.Plus, 
                                             [ Expr.String "terrabuild"
                                               Expr.Variable "terrabuild_project" ]) ]

        { ProjectFile.Extensions = Map [ "@dotnet", extDotnet ]
          ProjectFile.Project = project
          ProjectFile.Targets = Map [ "build", buildTarget ]
          ProjectFile.Locals = locals }

    let content = File.ReadAllText("TestFiles/PROJECT2")
    let project = Terrabuild.Configuration.FrontEnd.Project.parse content

    project
    |> should equal expectedProject
