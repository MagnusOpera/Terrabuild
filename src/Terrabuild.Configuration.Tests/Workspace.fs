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
            { DependsOn = Set [ "^build" ] }
        let targetDist =
            { DependsOn = Set [ "build" ] }
        let targetDummy =
            { DependsOn = Set.empty }

        let envRelease =
            { Variables = Map [ "configuration", "Release" ] }
        let envDummy =
            { Variables = Map.empty }

        let extDotnet =
            { Container = Some "mcr.microsoft.com/dotnet/sdk:8.0.101"
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "configuration" ] }
        let extDocker =
            { Container = None
              Script = None
              Defaults = Map.empty }

        { Space = Some "magnusopera/default"
          Targets = Map [ "build", targetBuild
                          "dist", targetDist
                          "dummy", targetDummy ]
          Environments = Map [ "release", envRelease
                               "dummy", envDummy ]
          Extensions = Map [ "dotnet", extDotnet
                             "docker", extDocker ] }


    let content = File.ReadAllText("TestFiles/WORKSPACE")
    let workspace = FrontEnd.parseWorkspace content

    workspace
    |> should equal expectedWorkspace
