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
            { DependsOn = Set [ "^build" ] |> Some
              Rebuild = None }
        let targetDist =
            { DependsOn = Set [ "build" ] |> Some
              Rebuild = Some true }
        let targetDummy =
            { DependsOn = None
              Rebuild = None }

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

        { Space = Some "magnusopera/default"
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
            { DependsOn = Set [ "^build" ] |> Some
              Rebuild = None }
        let targetDist =
            { DependsOn = Set [ "build" ] |> Some
              Rebuild = Some true }
        let targetDummy =
            { DependsOn = None
              Rebuild = None }

        let envRelease =
            { Variables = Map [ "configuration", Expr.String "Release" ] }
        let envDummy =
            { Variables = Map.empty }
        let envSecret =
            { Variables = Map [ "secret", Expr.Number 123456 ]}

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

        { Space = Some "magnusopera/default"
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
