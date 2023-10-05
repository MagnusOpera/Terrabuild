open Argu
open System
open CLI

let runTarget wsDir target noCache shared =
    let config = Configuration.read wsDir shared
    let graph = Graph.buildGraph config target
    let cache = BuildCache.Cache(None)
    let buildInfo = Build.run config graph noCache cache

    let jsonBuildInfo = Json.Serialize buildInfo
    printfn $"{jsonBuildInfo}"


let targetShortcut target (tbResult: ParseResults<TerrabuildArgs>) =
    let buildResult = tbResult.GetResult(TerrabuildArgs.Build)

    let wsDir =
        match tbResult.TryGetResult(TerrabuildArgs.Workspace) with
        | Some workspace -> workspace
        | _ -> "."

    let shared =
        match tbResult.TryGetResult(TerrabuildArgs.Shared) with
        | Some TerrabuildArgs.Shared -> true
        | _ -> false

    let noCache =
        match buildResult.TryGetResult(BuildArgs.NoCache) with
        | Some _ -> true
        | _ -> false

    runTarget wsDir target noCache shared


let target (tbResult: ParseResults<TerrabuildArgs>) =
    let targetResult = tbResult.GetResult(TerrabuildArgs.Run)

    let wsDir =
        match tbResult.TryGetResult(TerrabuildArgs.Workspace) with
        | Some workspace -> workspace
        | _ -> "."

    let shared =
        match tbResult.TryGetResult(TerrabuildArgs.Shared) with
        | Some TerrabuildArgs.Shared -> true
        | _ -> false

    let noCache =
        match targetResult.TryGetResult(RunArgs.NoCache) with
        | Some _ -> true
        | _ -> false

    let target = targetResult.GetResult(RunArgs.Target)

    runTarget wsDir target noCache shared


let errorHandler = ProcessExiter()
let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild", errorHandler = errorHandler)
match parser.ParseCommandLine() with
| p when p.Contains(TerrabuildArgs.Build) -> p |> targetShortcut "build"
| p when p.Contains(TerrabuildArgs.Dist) -> p |> targetShortcut "dist"
| p when p.Contains(TerrabuildArgs.Serve) -> p |> targetShortcut "serve"
| p when p.Contains(TerrabuildArgs.Run) -> p |> target
| _ -> printfn $"{parser.PrintUsage()}"
