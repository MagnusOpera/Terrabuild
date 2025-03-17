module Terrabuild.Configuration.Tests.Project

open System.IO
open NUnit.Framework
open FsUnit

open AST
open AST.Project

open Terrabuild.Expressions

[<Test>]
let parseProject() =
    let expectedProject =
        let project =
            { ProjectBlock.Dependencies = Set [ Expr.String "../../libraries/shell-lib" ]
              ProjectBlock.Links = Set.empty
              ProjectBlock.Outputs = Set [ Expr.String "dist" ]
              ProjectBlock.Ignores = Set.empty
              ProjectBlock.Includes = Set.empty
              ProjectBlock.Labels = Set [ Expr.String "app"; Expr.String "dotnet" ]
              ProjectBlock.Init = Some "@dotnet" }

        let extDotnet =
            { Container = None
              Platform = None
              Variables = Set.empty
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "configuration" ] }        
        let extDocker =
            { Container = None
              Platform = None
              Variables = Set [ Expr.String "ARM_TENANT_ID" ]
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "configuration"
                               "image", Expr.String "ghcr.io/magnusopera/dotnet-app" ] }
        let extDummy =
            { Container = None
              Platform = None
              Variables = Set.empty
              Script = "dummy.fsx" |> Expr.String |> Some
              Defaults = Map.empty }

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
                                      "docker", targetDocker ] }

    let content = File.ReadAllText("TestFiles/PROJECT")
    let project = FrontEnd.Project.parse content

    project
    |> should equal expectedProject
    
[<Test>]
let parseProject2() =
    let expectedProject =
        let project =
            { ProjectBlock.Dependencies = Set.empty
              ProjectBlock.Links = Set.empty
              ProjectBlock.Outputs = Set.empty
              ProjectBlock.Ignores = Set.empty
              ProjectBlock.Includes = Set.empty
              ProjectBlock.Labels = Set.empty
              ProjectBlock.Init = Some "@dotnet" }

        let extDotnet =
            { Container = None
              Platform = None
              Variables = Set.empty
              Script = None
              Defaults = Map.empty }        

        let buildTarget = 
            { TargetBlock.DependsOn = None
              TargetBlock.Rebuild = Some (Expr.Bool true)
              TargetBlock.Outputs = Set [ Expr.Function (Function.Format,
                                                         [ Expr.String "{0}{1}"
                                                           Expr.Variable "wildcard"
                                                           Expr.String ".dll" ])] |> Some
              TargetBlock.Cache = None
              TargetBlock.Steps = [ { Extension = "@dotnet"; Command = "build"; Parameters = Map.empty } ] }

        { ProjectFile.Extensions = Map [ "@dotnet", extDotnet ]
          ProjectFile.Project = project
          ProjectFile.Targets = Map [ "build", buildTarget ]  }

    let content = File.ReadAllText("TestFiles/PROJECT2")
    let project = FrontEnd.Project.parse content

    project
    |> should equal expectedProject
