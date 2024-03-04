module Terrabuild.Parser.Tests.Project

open System.IO
open NUnit.Framework
open FsUnit

open Terrabuild.Parser.AST
open Terrabuild.Parser.Project.AST
open Terrabuild.Expressions

[<Test>]
let parseProject() =
    let expectedProject =
        let dotnetExt =
            { Container = None
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "configuration" ] }
        
        let dockerExt =
            { Container = None
              Script = None
              Defaults = Map [ "configuration", Expr.Variable "configuration"
                               "image", Expr.String "ghcr.io/magnusopera/dotnet-app" ] }

        let configuration =
            { Dependencies = Set [ "../../libraries/shell-lib" ] 
              Outputs = Set [ "dist" ]
              Ignores = Set.empty
              Labels = Set [ "app"; "dotnet" ]
              Parser = Some "dotnet" }

        let buildTarget = 
            { DependsOn = Set [ "dist" ] |> Some
              Steps = [ { Extension = "dotnet"; Command = "build"; Parameters = Map.empty } ] }

        let distTarget =
            { DependsOn = None
              Steps = [ { Extension = "dotnet"; Command = "build"; Parameters = Map.empty }
                        { Extension = "dotnet"; Command = "publish"; Parameters = Map.empty } ] }

        let dockerTarget =
            { DependsOn = None 
              Steps = [ { Extension = "shell"; Command = "echo"
                          Parameters = Map [ "message", Expr.Function (Function.Trim,
                                                                       [ Expr.Function (Function.Plus,
                                                                                        [ Expr.String "building project1 "
                                                                                          Expr.Variable "configuration" ]) ]) ] }
                        { Extension = "docker"; Command = "build"
                          Parameters = Map [ "arguments", Expr.Map (Map [ "config", Expr.String "Release"]) ] } ] }

        { Extensions = Map [ "dotnet", dotnetExt
                             "docker", dockerExt ]
          Configuration = configuration
          Targets = Map [ "build", buildTarget
                          "dist", distTarget
                          "docker", dockerTarget ] }

    let content = File.ReadAllText("PROJECT")
    let project = FrontEnd.parseProject content

    project
    |> should equal expectedProject
    