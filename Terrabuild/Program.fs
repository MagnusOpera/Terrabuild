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


let targetShortcut target (buildArgs: ParseResults<RunArgs>) =
    let shared = buildArgs.TryGetResult(RunArgs.Shared) |> Option.isSome
    let wsDir = buildArgs.GetResult(RunArgs.Workspace, defaultValue = ".")
    let options = { ParallelBuild.BuildOptions.NoCache = buildArgs.Contains(RunArgs.NoCache)
                    ParallelBuild.BuildOptions.MaxConcurrency = buildArgs.GetResult(RunArgs.Parallel, defaultValue = 4)
                    ParallelBuild.BuildOptions.Retry = buildArgs.Contains(RunArgs.Retry) }
    runTarget wsDir target shared options


let target (targetArgs: ParseResults<TargetArgs>) =
    let wsDir = targetArgs.GetResult(TargetArgs.Workspace, defaultValue = ".")
    let shared = targetArgs.TryGetResult(TargetArgs.Shared) |> Option.isSome
    let target = targetArgs.GetResult(TargetArgs.Target)
    let options = { ParallelBuild.BuildOptions.NoCache = targetArgs.Contains(TargetArgs.NoCache)
                    ParallelBuild.BuildOptions.MaxConcurrency = targetArgs.GetResult(TargetArgs.Parallel, defaultValue = 4)
                    ParallelBuild.BuildOptions.Retry = targetArgs.Contains(TargetArgs.Retry) }
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
