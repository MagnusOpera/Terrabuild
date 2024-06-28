module Terrabuild.Core.Tests
open FsUnit
open NUnit.Framework
open Configuration
open Graph
open System

[<Test>]
let ``create same cluster on different layers``() =
    let wsDir = FS.combinePath NUnit.Framework.TestContext.CurrentContext.WorkDirectory "TestFiles/cluster-layers"
    FS.combinePath wsDir ".terrabuild" |> IO.createDirectory
    Environment.CurrentDirectory <- wsDir
    let sourceControl = SourceControls.Local()
    let options = { Options.WhatIf = false
                    Options.Debug = false
                    Options.MaxConcurrency = 1
                    Options.Force = true
                    Options.Retry = false
                    Options.StartedAt = DateTime.UtcNow
                    Options.IsLog = false
                    Options.NoContainer = false
                    Options.NoBatch = false }
    let configuration = Configuration.read wsDir "default" None None None Map.empty sourceControl options
    let graph = Graph.create configuration (Set.singleton "build") options
    let graph = Graph.markRequired graph options
    let graph = Graph.optimize configuration graph options
    ()
