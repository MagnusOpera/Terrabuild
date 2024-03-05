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
        let configuration = 
            { Storage = Some "azureblob"
              SourceControl = Some "github" }

        let buildTarget = 
            { DependsOn = Set [ "^build" ] }
        let distTarget =
            { DependsOn = Set [ "build" ] }
        
        let envRelease =
            { Variables = Map [ "configuration", "Release" ] }

        let dotnetExt =
            { Container = Some "mcr.microsoft.com/dotnet/sdk:8.0.101"
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "configuration" ] }
        let dockerExt =
            { Container = None
              Script = None
              Defaults = Map.empty }

        { Configuration = configuration
          Targets = Map [ "build", buildTarget
                          "dist", distTarget ]
          Environments = Map [ "release", envRelease]
          Extensions = Map [ "dotnet", dotnetExt
                             "docker", dockerExt ] }


    let content = File.ReadAllText("TestFiles/WORKSPACE")
    let workspace = FrontEnd.parseWorkspace content

    workspace
    |> should equal expectedWorkspace
