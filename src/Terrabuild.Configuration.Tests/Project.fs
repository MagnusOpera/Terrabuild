module Terrabuild.Configuration.Tests.Project

open System.IO
open NUnit.Framework
open FsUnit

open Terrabuild.Configuration
open Terrabuild.Configuration.AST
open Terrabuild.Configuration.Project.AST

open Terrabuild.Expressions

[<Test>]
let parseProject() =
    let expectedProject =
        let project =
            { Project.Container = None
              Project.Dependencies = Set [ "../../libraries/shell-lib" ]
              Project.Links = Set.empty
              Project.Outputs = Set [ "dist" ]
              Project.Ignores = Set.empty
              Project.Includes = Set.empty
              Project.Labels = Set [ "app"; "dotnet" ]
              Project.Init = Some "@dotnet" }

        let extDotnet =
            { Container = None
              Variables = Set.empty
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "configuration" ] }        
        let extDocker =
            { Container = None
              Variables = Set [ "ARM_TENANT_ID" ]
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "configuration"
                               "image", Expr.String "ghcr.io/magnusopera/dotnet-app" ] }
        let extDummy =
            { Container = None
              Variables = Set.empty
              Script = Some "dummy.fsx"
              Defaults = Map.empty }

        let targetBuild = 
            { Target.DependsOn = Set [ "dist" ] |> Some
              Target.Rebuild = None
              Target.Outputs = None
              Target.Cache = None
              Target.Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty } ] }
        let targetDist =
            { Target.DependsOn = None
              Target.Rebuild = None
              Target.Outputs = None
              Target.Cache = None
              Target.Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty }
                               { Extension = "@dotnet"; Command = "publish"; Parameters = Map.empty } ] }
        let targetDocker =
            { Target.DependsOn = None
              Target.Rebuild = Some (Expr.Bool false)
              Target.Outputs = None
              Target.Cache = Some Cacheability.Always
              Target.Steps = [ { Extension = "@shell"; Command = "echo"
                                 Parameters = Map [ "arguments", Expr.Function (Function.Trim,
                                                                                [ Expr.Function (Function.Plus,
                                                                                                 [ Expr.String "building project1 "
                                                                                                   Expr.Variable "configuration" ]) ]) ] }
                               { Extension = "@docker"; Command = "build"
                                 Parameters = Map [ "arguments", Expr.Map (Map [ "config", Expr.String "Release"]) ] } ] }

        { ProjectFile.Extensions = Map [ "@dotnet", extDotnet
                                         "@docker", extDocker
                                         "dummy", extDummy ]
          ProjectFile.Project = project
          ProjectFile.Targets = Map [ "build", targetBuild
                                      "dist", targetDist
                                      "docker", targetDocker ] }

    let content = File.ReadAllText("TestFiles/PROJECT")
    let project = FrontEnd.parseProject content

    project
    |> should equal expectedProject
    
[<Test>]
let parseProject2() =
    let expectedProject =
        let project =
            { Project.Container = None
              Project.Dependencies = Set.empty
              Project.Links = Set.empty
              Project.Outputs = Set.empty
              Project.Ignores = Set.empty
              Project.Includes = Set.empty
              Project.Labels = Set.empty
              Project.Init = Some "@dotnet" }

        let buildTarget = 
            { Target.DependsOn = None
              Target.Rebuild = Some (Expr.Bool true)
              Target.Outputs = Set [ "*.dll" ] |> Some
              Target.Cache = None
              Target.Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty } ] }

        { ProjectFile.Extensions = Map.empty
          ProjectFile.Project = project
          ProjectFile.Targets = Map [ "build", buildTarget ]  }

    let content = File.ReadAllText("TestFiles/PROJECT2")
    let project = FrontEnd.parseProject content

    project
    |> should equal expectedProject
