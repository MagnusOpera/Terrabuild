module Terrabuild.Parser.Tests.Workspace
open System.IO
open NUnit.Framework
open FsUnit
open Terrabuild.Parser.AST
open Terrabuild.Parser.Workspace.AST
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
            { Variables = Map [ "configuration", Expr.String "Release" ] }

        let dotnetExt =
            { Container = Some "mcr.microsoft.com/dotnet/sdk:8.0.101"
              Script = None
              Init = Map [ "configuration", Expr.Variable "configuration" ]
              Defaults = Map.empty }
        let dockerExt =
            { Container = None
              Script = None
              Init = Map.empty
              Defaults = Map.empty }

        { Configuration = configuration
          Targets = Map [ "build", buildTarget
                          "dist", distTarget ]
          Environments = Map [ "release", envRelease]
          Extensions = Map [ "dotnet", dotnetExt
                             "docker", dockerExt ] }


    let content = File.ReadAllText("WORKSPACE")
    let workspace = FrontEnd.parseWorkspace content

    workspace
    |> should equal expectedWorkspace
