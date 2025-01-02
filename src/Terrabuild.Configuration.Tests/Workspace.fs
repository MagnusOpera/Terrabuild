module Terrabuild.Parser.Tests.Workspace
open System.IO
open NUnit.Framework
open FsUnit

open Terrabuild.Configuration
open Terrabuild.Configuration.AST
open Terrabuild.Configuration.Workspace.AST
open Terrabuild.Expressions

[<Test>]
let parseWorkspace() =
    let expectedWorkspace =
        let targetBuild = 
            { DependsOn = Set [ "^build" ]
              Rebuild = Expr.Bool false }
        let targetDist =
            { DependsOn = Set [ "build" ]
              Rebuild = Expr.Bool true }
        let targetDummy =
            { DependsOn = Set.empty
              Rebuild = Expr.Bool false }

        let envRelease =
            { Variables = Map [ "configuration", Expr.String "Release" ] }
        let envDummy =
            { Variables = Map.empty }

        let extDotnet =
            { Container = Some "mcr.microsoft.com/dotnet/sdk:8.0.101"
              Variables = Set.empty
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "configuration" ] }
        let extDocker =
            { Container = None
              Variables = Set.empty
              Script = None
              Defaults = Map.empty }

        { Workspace = { Space = Some "magnusopera/default"; Ignores = Set ["**/node_modules"] }
          Targets = Map [ "build", targetBuild
                          "dist", targetDist
                          "dummy", targetDummy ]
          Configurations = Map [ "release", envRelease
                                 "dummy", envDummy ]
          Extensions = Map [ "dotnet", extDotnet
                             "docker", extDocker ] }


    let content = File.ReadAllText("TestFiles/WORKSPACE")
    let workspace = FrontEnd.parseWorkspace content

    workspace
    |> should equal expectedWorkspace

[<Test>]
let parseWorkspace2() =
    let expectedWorkspace =
        let targetBuild = 
            { DependsOn = Set [ "^build" ]
              Rebuild = Expr.Bool false }
        let targetDist =
            { DependsOn = Set [ "build" ]
              Rebuild = Expr.Bool true }
        let targetDummy =
            { DependsOn = Set.empty
              Rebuild = Expr.Bool false }

        let envRelease =
            { Variables = Map [ "configuration", Expr.String "Release"
                                "map", Expr.Map (Map [ "toto", Expr.Number 42
                                                       "titi", Expr.String "tagada" ])
                                "list", Expr.List [ Expr.Number 1
                                                    Expr.Function (Function.Plus, [ Expr.Number 2; Expr.Number 3 ])
                                                    Expr.String "tutu"
                                                    Expr.Function (Function.Coalesce, [ Expr.Nothing; Expr.Number 42 ])
                                                    Expr.Function (Function.NotEqual, [Expr.Number 42; Expr.String "toto" ]) ] ] }
        let envDummy =
            { Variables = Map.empty }
        let envSecret =
            { Variables = Map [ "secret", Expr.Function (Function.Ternary, [ Expr.Function (Function.Equal, [ Expr.Function (Function.Item, [Expr.Variable "map"; Expr.String "toto"])
                                                                                                              Expr.String "prod"])
                                                                             Expr.Number 1234
                                                                             Expr.Number 5678 ]) 
                                "secret2", Expr.Function (Function.Item, [Expr.Variable "list"; Expr.Number 2]) 
                                "secret3", Expr.Function (Function.Plus, [
                                    Expr.Function (Function.Not, [ Expr.Bool false ])
                                    Expr.Function (Function.Not, [ Expr.Bool true ]) ])
                                "secret4", Expr.Function (Function.Format, [
                                    Expr.String "1"
                                    Expr.Number 2
                                    Expr.Variable "toto"
                                    Expr.Bool true
                                    Expr.Nothing ])
                                "secret5", Expr.Function (Function.ToString, [Expr.Function (Function.Plus, [Expr.Function (Function.Plus, [Expr.Number 40; Expr.Number 1]); Expr.Number 2])])
                                "secret6", Expr.Function (Function.Or, [ Expr.Function (Function.And, [Expr.Bool true; Expr.Bool false])
                                                                         Expr.Bool true ])
                              ] }

        let extDotnet =
            { Container = Some "mcr.microsoft.com/dotnet/sdk:8.0.101"
              Variables = Set.empty
              Script = None
              Defaults = Map [ "configuration1", Expr.Function (Function.Item, [Expr.Variable "map"; Expr.String "toto"])
                               "configuration2", Expr.Function (Function.TryItem, [Expr.Variable "map"; Expr.String "titi"])
                               "configuration3", Expr.Function (Function.Replace, [Expr.String "toto titi"; Expr.String "toto"; Expr.String "titi"]) ] }
        let extDocker =
            { Container = None
              Variables = Set.empty
              Script = None
              Defaults = Map.empty }

        { Workspace = { Space = Some "magnusopera/default"; Ignores = Set.empty }
          Targets = Map [ "build", targetBuild
                          "dist", targetDist
                          "dummy", targetDummy ]
          Configurations = Map [ "default", envSecret
                                 "release", envRelease
                                 "dummy", envDummy ]
          Extensions = Map [ "dotnet", extDotnet
                             "docker", extDocker ] }


    let content = File.ReadAllText("TestFiles/WORKSPACE2")
    let workspace = FrontEnd.parseWorkspace content

    workspace
    |> should equal expectedWorkspace
