open Argu
open System
open CLI

let runTarget wsDir target shared options =
    let config = Configuration.read wsDir shared
    let graph = Graph.buildGraph config target
    let cache = BuildCache.Cache(config.Storage)
    let buildLevels = BuildOptimizer.optimize graph
    let buildInfo = ParallelBuild.run config buildLevels cache options
    let jsonBuildInfo = Json.Serialize buildInfo
    printfn $"{jsonBuildInfo}"


let targetShortcut target (buildArgs: ParseResults<BuildArgs>) =
    let shared = buildArgs.TryGetResult(BuildArgs.Shared) |> Option.isSome
    let wsDir = buildArgs.GetResult(BuildArgs.Workspace, defaultValue = ".")
    let options = { ParallelBuild.BuildOptions.NoCache = buildArgs.Contains(BuildArgs.NoCache)
                    ParallelBuild.BuildOptions.MaxBuilds = buildArgs.GetResult(BuildArgs.Parallel, defaultValue = 4) }
    runTarget wsDir target shared options


let target (targetArgs: ParseResults<RunArgs>) =
    let wsDir = targetArgs.GetResult(RunArgs.Workspace, defaultValue = ".")
    let shared = targetArgs.TryGetResult(RunArgs.Shared) |> Option.isSome
    let target = targetArgs.GetResult(RunArgs.Target)
    let options = { ParallelBuild.BuildOptions.NoCache = targetArgs.Contains(RunArgs.NoCache)
                    ParallelBuild.BuildOptions.MaxBuilds = targetArgs.GetResult(RunArgs.Parallel, defaultValue = 4) }
    runTarget wsDir target shared options


let clear (clearArgs: ParseResults<ClearArgs>) =
    if clearArgs.Contains(ClearArgs.BuildCache) then BuildCache.clearBuildCache()

let errorHandler = ProcessExiter()
let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild", errorHandler = errorHandler)
match parser.ParseCommandLine() with
| p when p.Contains(TerrabuildArgs.Build) -> p.GetResult(TerrabuildArgs.Build) |> targetShortcut "build"
| p when p.Contains(TerrabuildArgs.Test) -> p.GetResult(TerrabuildArgs.Test) |> targetShortcut "test"
| p when p.Contains(TerrabuildArgs.Dist) -> p.GetResult(TerrabuildArgs.Dist) |> targetShortcut "dist"
| p when p.Contains(TerrabuildArgs.Serve) -> p.GetResult(TerrabuildArgs.Serve) |> targetShortcut "serve"
| p when p.Contains(TerrabuildArgs.Run) -> p.GetResult(TerrabuildArgs.Run) |> target
| p when p.Contains(TerrabuildArgs.Clear) -> p.GetResult(TerrabuildArgs.Clear) |> clear
| _ -> printfn $"{parser.PrintUsage()}"
