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
        let configuration =
            { Dependencies = Set [ "../../libraries/shell-lib" ] |> Some
              Outputs = Set [ "dist" ] |> Some
              Ignores = None
              Labels = Set [ "app"; "dotnet" ]
              Init = Some "@dotnet" }


        let extDotnet =
            { Container = None
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "configuration" ] }        
        let extDocker =
            { Container = None
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "configuration"
                               "image", Expr.String "ghcr.io/magnusopera/dotnet-app" ] }
        let extDummy =
            { Container = None
              Script = Some "dummy.fsx"
              Defaults = Map.empty }

        let targetBuild = 
            { DependsOn = Set [ "dist" ] |> Some
              Outputs = None
              Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty } ] }
        let targetDist =
            { DependsOn = None
              Outputs = None
              Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty }
                        { Extension = "@dotnet"; Command = "publish"; Parameters = Map.empty } ] }
        let targetDocker =
            { DependsOn = None 
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
          Configuration = configuration
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
        let configuration =
            { Dependencies = None
              Outputs = None
              Ignores = None
              Labels = Set.empty
              Init = Some "@dotnet" }

        let buildTarget = 
            { DependsOn = None
              Outputs = Some (Set [ "*.dll" ])
              Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty } ] }

        { Extensions = Map.empty
          Configuration = configuration
          Targets = Map [ "build", buildTarget ]  }

    let content = File.ReadAllText("TestFiles/PROJECT2")
    let project = FrontEnd.parseProject content

    project
    |> should equal expectedProject
