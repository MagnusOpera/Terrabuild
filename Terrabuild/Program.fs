open Argu
open System
open CLI

let runTarget wsDir target noCache =
    printfn $"Running target '{target}'"

    let config = Configuration.read wsDir
    let graph = Graph.buildGraph config target
    let buildInfo = Build.run config graph noCache
    printfn $"{buildInfo}"


let build (tbResult: ParseResults<TerrabuildArgs>) =
    let buildResult = tbResult.GetResult(TerrabuildArgs.Build)

    let wsDir =
        match tbResult.TryGetResult(TerrabuildArgs.Workspace) with
        | Some workspace -> workspace
        | _ -> "."

    let noCache =
        match buildResult.TryGetResult(BuildArgs.NoCache) with
        | Some _ -> true
        | _ -> false

    let tags = buildResult.GetResults(BuildArgs.Tag)

    runTarget wsDir "build" noCache

let target (tbResult: ParseResults<TerrabuildArgs>) =
    let targetResult = tbResult.GetResult(TerrabuildArgs.Run)

    let wsDir =
        match tbResult.TryGetResult(TerrabuildArgs.Workspace) with
        | Some workspace -> workspace
        | _ -> "."

    let noCache =
        match targetResult.TryGetResult(RunArgs.NoCache) with
        | Some _ -> true
        | _ -> false

    let target = targetResult.GetResult(RunArgs.Target)

    let tags = targetResult.GetResults(RunArgs.Tag)

    runTarget wsDir target noCache


let errorHandler = ProcessExiter()
let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild", errorHandler = errorHandler)
match parser.ParseCommandLine() with
| p when p.Contains(TerrabuildArgs.Build) -> p |> build
| p when p.Contains(TerrabuildArgs.Run) -> p |> target
| _ -> printfn $"{parser.PrintUsage()}"
