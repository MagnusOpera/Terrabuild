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


let targetShortcut target (buildArgs: ParseResults<BuildArgs>) =
    let shared = buildArgs.TryGetResult(BuildArgs.Shared) |> Option.isSome

    let wsDir =
        match buildArgs.TryGetResult(BuildArgs.Workspace) with
        | Some workspace -> workspace
        | _ -> "."

    let noCache =
        match buildArgs.TryGetResult(BuildArgs.NoCache) with
        | Some _ -> true
        | _ -> false

    runTarget wsDir target noCache shared


let target (targetArgs: ParseResults<RunArgs>) =
    let wsDir =
        match targetArgs.TryGetResult(RunArgs.Workspace) with
        | Some workspace -> workspace
        | _ -> "."

    let shared = targetArgs.TryGetResult(RunArgs.Shared) |> Option.isSome

    let noCache =
        match targetArgs.TryGetResult(RunArgs.NoCache) with
        | Some _ -> true
        | _ -> false

    let target = targetArgs.GetResult(RunArgs.Target)

    runTarget wsDir target noCache shared


let clear (clearArgs: ParseResults<ClearArgs>) =
    match clearArgs.TryGetResult(ClearArgs.BuildCache) with
    | Some _ -> BuildCache.clearBuildCache()
    | _ -> ()


let errorHandler = ProcessExiter()
let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild", errorHandler = errorHandler)
match parser.ParseCommandLine() with
| p when p.Contains(TerrabuildArgs.Build) -> p.GetResult(TerrabuildArgs.Build) |> targetShortcut "build"
| p when p.Contains(TerrabuildArgs.Dist) -> p.GetResult(TerrabuildArgs.Dist) |> targetShortcut "dist"
| p when p.Contains(TerrabuildArgs.Serve) -> p.GetResult(TerrabuildArgs.Serve) |> targetShortcut "serve"
| p when p.Contains(TerrabuildArgs.Run) -> p.GetResult(TerrabuildArgs.Run) |> target
| p when p.Contains(TerrabuildArgs.Clear) -> p.GetResult(TerrabuildArgs.Clear) |> clear
| _ -> printfn $"{parser.PrintUsage()}"
