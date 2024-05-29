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
          Configurations = Map [ "release", envRelease
                                 "dummy", envDummy ]
          Environments = Map.empty
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
            { Variables = Map [ "secret", Expr.Number 0 ]}
        let envSecretProd =
            { Variables = Map [ "secret", Expr.Number 123456 ]}

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
          Configurations = Map [ "release", envRelease
                                 "dummy", envDummy ]
          Environments = Map [ "default", envSecret
                               "prod", envSecretProd ]
          Extensions = Map [ "dotnet", extDotnet
                             "docker", extDocker ] }


    let content = File.ReadAllText("TestFiles/WORKSPACE2")
    let workspace = FrontEnd.parseWorkspace content

    workspace
    |> should equal expectedWorkspace
