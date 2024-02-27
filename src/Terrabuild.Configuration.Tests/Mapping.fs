module Terrabuild.Configuration.Tests
open System.IO
open NUnit.Framework
open FsUnit
open AST

[<Test>]
let test_workspace() =
    let content = File.ReadAllText("WORKSPACE")
    let attributes = FrontEnd.parse content

    let value = Mapper.mapRecord None (typeof<Workspace.WorkspaceConfiguration>) attributes
    printf $"{value}"
    Assert.IsFalse(true)

[<Test>]
let test_project() =
    let content = File.ReadAllText("PROJECT")
    let attributes = FrontEnd.parse content

    let value = Mapper.mapRecord None (typeof<Project.ProjectConfiguration>) attributes
    printf $"{value}"
    Assert.IsFalse(true)
