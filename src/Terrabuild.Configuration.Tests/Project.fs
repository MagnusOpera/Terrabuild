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
            { Dependencies = Set [ "../../libraries/shell-lib" ] |> Some
              Links = None
              Outputs = Set [ "dist" ] |> Some
              Ignores = None
              Includes = None
              Labels = Set [ "app"; "dotnet" ]
              Init = Some "@dotnet" }

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
            { DependsOn = Set [ "dist" ] |> Some
              Rebuild = None
              Outputs = None
              Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty } ] }
        let targetDist =
            { DependsOn = None
              Rebuild = None
              Outputs = None
              Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty }
                        { Extension = "@dotnet"; Command = "publish"; Parameters = Map.empty } ] }
        let targetDocker =
            { DependsOn = None
              Rebuild = Some (Expr.Boolean false)
              Outputs = None
              Steps = [ { Extension = "@shell"; Command = "echo"
                          Parameters = Map [ "arguments", Expr.Function (Function.Trim,
                                                                         [ Expr.Function (Function.Plus,
                                                                                          [ Expr.String "building project1 "
                                                                                            Expr.Variable "configuration" ]) ]) ] }
                        { Extension = "@docker"; Command = "build"
                          Parameters = Map [ "arguments", Expr.Map (Map [ "config", Expr.String "Release"]) ] } ] }

        { Extensions = Map [ "@dotnet", extDotnet
                             "@docker", extDocker
                             "dummy", extDummy ]
          Project = project
          Targets = Map [ "build", targetBuild
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
            { Dependencies = None
              Links = None
              Outputs = None
              Ignores = None
              Includes = None
              Labels = Set.empty
              Init = Some "@dotnet" }

        let buildTarget = 
            { DependsOn = None
              Rebuild = Some (Expr.Boolean true)
              Outputs = Set [ "*.dll" ] |> Some
              Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty } ] }

        { Extensions = Map.empty
          Project = project
          Targets = Map [ "build", buildTarget ]  }

    let content = File.ReadAllText("TestFiles/PROJECT2")
    let project = FrontEnd.parseProject content

    project
    |> should equal expectedProject
