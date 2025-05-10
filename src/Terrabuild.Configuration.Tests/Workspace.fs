module Terrabuild.Parser.Tests.Workspace
open System.IO
open NUnit.Framework
open FsUnit

open Terrabuild.Configuration.AST
open Terrabuild.Configuration.AST.Workspace
open Terrabuild.Expressions


[<Test>]
let parseWorkspace() =
    let expectedWorkspace =
        let targetBuild = 
            { TargetBlock.DependsOn = Set [ "^build" ]
              TargetBlock.Rebuild = Expr.Bool false }
        let targetDist =
            { TargetBlock.DependsOn = Set [ "build" ]
              TargetBlock.Rebuild = Expr.Bool true }
        let targetDummy =
            { TargetBlock.DependsOn = Set.empty
              TargetBlock.Rebuild = Expr.Bool false }

        let envRelease =
            { ConfigurationBlock.Variables = Map [ "configuration", Expr.String "Release" ] }
        let envDummy =
            { ConfigurationBlock.Variables = Map.empty }

        let extDotnet =
            { Container = Some (Expr.String "mcr.microsoft.com/dotnet/sdk:8.0.101")
              Platform = None
              Variables = Set.empty
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "configuration" ] }
        let extDocker =
            { Container = None
              Platform = None
              Variables = Set.empty
              Script = None
              Defaults = Map.empty }
        let extNpm =
            { Container = Some (Expr.String "node:20")
              Platform = None
              Variables = Set.empty
              Script = "scripts/npm.fsx" |> Expr.String |> Some
              Defaults = Map.empty }

        { WorkspaceFile.Workspace = { Id = "d7528db2-83e0-4164-8c8e-1e0d6d6357ca" |> Expr.String |> Some
                                      Ignores = Set [ Expr.String "**/node_modules" ] }
          WorkspaceFile.Targets = Map [ "build", targetBuild
                                        "dist", targetDist
                                        "dummy", targetDummy ]
          WorkspaceFile.Configurations = Map [ "release", envRelease
                                               "dummy", envDummy ]
          WorkspaceFile.Extensions = Map [ "dotnet", extDotnet
                                           "docker", extDocker
                                           "npmext", extNpm ] }


    let content = File.ReadAllText("TestFiles/WORKSPACE")
    let workspace = FrontEnd.Workspace.parse content

    workspace
    |> should equal expectedWorkspace

[<Test>]
let parseWorkspace2() =
    let expectedWorkspace =
        let targetBuild = 
            { TargetBlock.DependsOn = Set [ "^build" ]
              TargetBlock.Rebuild = Expr.Bool false }
        let targetDist =
            { TargetBlock.DependsOn = Set [ "build" ]
              TargetBlock.Rebuild = Expr.Bool true }
        let targetDummy =
            { TargetBlock.DependsOn = Set.empty
              TargetBlock.Rebuild = Expr.Bool false }

        let envRelease =
            { ConfigurationBlock.Variables = Map [ "configuration", Expr.String "Release"
                                                   "map", Expr.Map (Map [ "toto", Expr.Number 42
                                                                          "titi", Expr.String "tagada" ])
                                                   "list", Expr.List [ Expr.Number 1
                                                                       Expr.Function (Function.Plus, [ Expr.Number 2; Expr.Number 3 ])
                                                                       Expr.String "tutu"
                                                                       Expr.Function (Function.Coalesce, [ Expr.Nothing; Expr.Number 42 ])
                                                                       Expr.Function (Function.NotEqual, [Expr.Number 42; Expr.String "toto" ]) ] ] }
        let envDummy =
            { ConfigurationBlock.Variables = Map.empty }
        let envSecret =
            { ConfigurationBlock.Variables = Map [ "secret", Expr.Function (Function.Ternary, [ Expr.Function (Function.Equal, [ Expr.Function (Function.Item, [Expr.Variable "map"; Expr.String "toto"])
                                                                                                                                 Expr.String "prod"])
                                                                                                Expr.Number 1234
                                                                                                Expr.Number 5678 ]) 
                                                   "secret2", Expr.Function (Function.Item, [Expr.Variable "list"; Expr.Number 2]) 
                                                   "secret3", Expr.Function (Function.Plus, [
                                                       Expr.Function (Function.Not, [ Expr.Bool false ])
                                                       Expr.Function (Function.Not, [ Expr.Bool true ]) ])
                                                   "secret6", Expr.Function (Function.Or, [ Expr.Function (Function.And, [Expr.Bool true; Expr.Bool false])
                                                                                            Expr.Bool true ])
                                                   "secret7", Expr.Function (Function.Format,
                                                                             [ Expr.String "{0}{1}";
                                                                               Expr.Function (Function.Format,
                                                                                              [ Expr.String "{0}{1}{2}"
                                                                                                Expr.Function (Function.Format,
                                                                                                               [ Expr.String "{0}{1}"
                                                                                                                 Expr.String "hello "
                                                                                                                 Expr.Function (Function.Plus,
                                                                                                                                [ Expr.Variable "name"
                                                                                                                                  Expr.String "toto"])])
                                                                                                Expr.String " x "
                                                                                                Expr.Number 42 ])
                                                                               Expr.String " {^}" ])
                                                   "secret8", Expr.String "{ Hello \"!\" }"
                                                   "my-variable", Expr.Number 42
                                                 ] }

        let extDotnet =
            { Container = Some (Expr.String "mcr.microsoft.com/dotnet/sdk:8.0.101")
              Platform = None
              Variables = Set.empty
              Script = None
              Defaults = Map [ "configuration1", Expr.Function (Function.Item, [Expr.Variable "map"; Expr.String "toto"])
                               "configuration2", Expr.Function (Function.TryItem, [Expr.Variable "map"; Expr.String "titi"])
                               "configuration3", Expr.Function (Function.Replace, [Expr.String "toto titi"; Expr.String "toto"; Expr.String "titi"]) ] }
        let extDocker =
            { Container = None
              Platform = None
              Variables = Set.empty
              Script = None
              Defaults = Map.empty }

        { WorkspaceFile.Workspace = { Id = None; Ignores = Set.empty }
          WorkspaceFile.Targets = Map [ "build", targetBuild
                                        "dist", targetDist
                                        "dummy", targetDummy ]
          WorkspaceFile.Configurations = Map [ "default", envSecret
                                               "release", envRelease
                                               "dummy", envDummy ]
          WorkspaceFile.Extensions = Map [ "dotnet", extDotnet
                                           "docker", extDocker ] }


    let content = File.ReadAllText("TestFiles/WORKSPACE2")
    let workspace =FrontEnd.Workspace.parse content

    workspace
    |> should equal expectedWorkspace
