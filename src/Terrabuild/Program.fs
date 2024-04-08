open Argu
open CLI
open System
open Serilog

let rec dumpKnownException (ex: Exception) =
    seq {
        match ex with
        | :? Configuration.ConfigException as ex ->
            yield ex.Message
            yield! ex.InnerException |> dumpKnownException
        | null -> ()
        | _ ->
            yield ex.ToString()
            yield! ex.InnerException |> dumpKnownException
    }

let rec dumpUnknownException (ex: Exception) =
    seq {
        match ex with
        | :? Configuration.ConfigException as ex ->
            yield! ex |> dumpKnownException
        | null -> ()
        | _ -> yield ex.ToString()
    }



type TerrabuildExiter() =
    interface IExiter with
        member _.Name: string = "Process Exiter"

        member _.Exit(msg: string, errorCode: ErrorCode) =
            do
                Log.Fatal("Failed with {Message} and {ErrorCode}", msg, errorCode)
                msg |> Terminal.writeLine
                Terminal.showCursor()

            exit (int errorCode)

let launchDir = Environment.CurrentDirectory

let processCommandLine () =
    let errorHandler = TerrabuildExiter()
    let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild", errorHandler = errorHandler)
    let result = parser.ParseCommandLine()
    let debug = result.Contains(TerrabuildArgs.Debug)
    let whatIf = result.Contains(TerrabuildArgs.WhatIf)

    let logFile name = IO.combinePath launchDir $"terrabuild-debug.{name}"

    if debug then
        Log.Logger <-
            LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logFile "log")
                .CreateLogger()

    let runTarget wsDir target environment labels variables (options: Configuration.Options) =
        try
            let wsDir = wsDir |> IO.fullPath
            Environment.CurrentDirectory <- wsDir

            if options.Debug then
                let jsonOptions = Json.Serialize options
                jsonOptions |> IO.writeTextFile (logFile "options.json")

            let sourceControl = SourceControls.Factory.create options.Local
            let storage = Storages.Factory.create()

            $"{Ansi.Emojis.box} Reading configuration using environment {environment}" |> Terminal.writeLine
            let config = Configuration.read wsDir environment labels variables sourceControl storage options

            if options.Debug then
                let jsonConfig = Json.Serialize config
                jsonConfig |> IO.writeTextFile (logFile "config.json")

            $"{Ansi.Emojis.popcorn} Constructing graph" |> Terminal.writeLine
            let graph = Graph.buildGraph config target

            if options.Debug then
                let jsonGraph = Json.Serialize graph
                jsonGraph |> IO.writeTextFile (logFile "graph.json")
                let mermaid = Graph.graph graph |> String.join "\n"
                mermaid |> IO.writeTextFile (logFile "graph.mermaid")

            let cache = Cache.Cache(config.Storage) :> Cache.ICache
            let buildGraph = Graph.optimize config graph cache options
            if options.Debug then
                let jsonBuildGraph = Json.Serialize buildGraph
                jsonBuildGraph |> IO.writeTextFile (logFile "buildgraph.json")
                let mermaid = Graph.graph buildGraph |> String.join "\n"
                mermaid |> IO.writeTextFile (logFile "buildgraph.mermaid")

            if options.WhatIf then 0
            else
                let targets = graph.Targets |> String.join ","
                let targetLabel = if graph.Targets.Count > 1 then "targets" else "target"
                $"{Ansi.Emojis.rocket} Running {targetLabel} {targets}" |> Terminal.writeLine

                let buildNotification = Notification.BuildNotification() :> Build.IBuildNotification
                let build = Build.run config buildGraph cache buildNotification options
                buildNotification.WaitCompletion()

                if options.Debug then
                    let jsonBuild = Json.Serialize build
                    jsonBuild |> IO.writeTextFile (logFile "build.json")

                if build.Status = Build.Status.Success then 0
                else 5
        with
            | :? Configuration.ConfigException as ex ->
                Log.Fatal("Failed with {Exception}", ex)
                let reason = 
                    if options.Debug then ex.ToString()
                    else dumpUnknownException ex |> String.join "\n   "
                $"{Ansi.Emojis.explosion} {reason}" |> Terminal.writeLine
                5


    let scaffold (scaffoldArgs: ParseResults<ScaffoldArgs>) =
        try
            let wsDir = scaffoldArgs.GetResult(ScaffoldArgs.Workspace, defaultValue = ".")
            let force = scaffoldArgs.Contains(ScaffoldArgs.Force)
            Scalffold.scaffold wsDir force
            0
        with
        | ex ->
            let reason = ex.ToString()
            $"{Ansi.Emojis.explosion} {reason}" |> Terminal.writeLine
            5

    let targetShortcut target (buildArgs: ParseResults<RunArgs>) =
        let wsDir = buildArgs.GetResult(RunArgs.Workspace, defaultValue = ".")
        let environment = buildArgs.TryGetResult(RunArgs.Environment) |> Option.defaultValue "default" |> String.toLower
        let labels = buildArgs.TryGetResult(RunArgs.Label) |> Option.map (fun labels -> labels |> Seq.map String.toLower |> Set)
        let variables = buildArgs.GetResults(RunArgs.Variable) |> Seq.map (fun (k, v) -> k |> String.toLower, v) |> Map
        let options = { Configuration.Options.WhatIf = whatIf
                        Configuration.Options.Debug = debug
                        Configuration.Options.Force = buildArgs.Contains(RunArgs.Force)
                        Configuration.Options.Local = buildArgs.Contains(RunArgs.Local)
                        Configuration.Options.MaxConcurrency = buildArgs.GetResult(RunArgs.Parallel, defaultValue = Environment.ProcessorCount / 2)
                        Configuration.Options.Retry = buildArgs.Contains(RunArgs.Retry)
                        Configuration.Options.StartedAt = DateTime.UtcNow }
        runTarget wsDir (Set.singleton target) environment labels variables options


    let target (targetArgs: ParseResults<TargetArgs>) =
        let targets = targetArgs.GetResult(TargetArgs.Target) |> Seq.map String.toLower
        let wsDir = targetArgs.GetResult(TargetArgs.Workspace, defaultValue = ".")
        let environment = targetArgs.TryGetResult(TargetArgs.Environment) |> Option.defaultValue "default" |> String.toLower
        let labels = targetArgs.TryGetResult(TargetArgs.Label) |> Option.map (fun labels -> labels |> Seq.map String.toLower |> Set)
        let variables = targetArgs.GetResults(TargetArgs.Variable) |> Seq.map (fun (k, v) -> k |> String.toLower, v) |> Map
        let options = { Configuration.Options.WhatIf = whatIf
                        Configuration.Options.Debug = debug
                        Configuration.Options.Force = targetArgs.Contains(TargetArgs.Force)
                        Configuration.Options.Local = targetArgs.Contains(TargetArgs.Local)
                        Configuration.Options.MaxConcurrency = targetArgs.GetResult(TargetArgs.Parallel, defaultValue = Environment.ProcessorCount / 2)
                        Configuration.Options.Retry = targetArgs.Contains(TargetArgs.Retry)
                        Configuration.Options.StartedAt = DateTime.UtcNow }
        runTarget wsDir (Set targets) environment labels variables options


    let clear (clearArgs: ParseResults<ClearArgs>) =
        if clearArgs.Contains(ClearArgs.BuildCache) then Cache.clearBuildCache()

    match result with
    | p when p.Contains(TerrabuildArgs.Scaffold) -> p.GetResult(TerrabuildArgs.Scaffold) |> scaffold
    | p when p.Contains(TerrabuildArgs.Build) -> p.GetResult(TerrabuildArgs.Build) |> targetShortcut "build"
    | p when p.Contains(TerrabuildArgs.Test) -> p.GetResult(TerrabuildArgs.Test) |> targetShortcut "test"
    | p when p.Contains(TerrabuildArgs.Dist) -> p.GetResult(TerrabuildArgs.Dist) |> targetShortcut "dist"
    | p when p.Contains(TerrabuildArgs.Publish) -> p.GetResult(TerrabuildArgs.Publish) |> targetShortcut "publish"
    | p when p.Contains(TerrabuildArgs.Deploy) -> p.GetResult(TerrabuildArgs.Publish) |> targetShortcut "deploy"
    | p when p.Contains(TerrabuildArgs.Serve) -> p.GetResult(TerrabuildArgs.Serve) |> targetShortcut "serve"
    | p when p.Contains(TerrabuildArgs.Run) -> p.GetResult(TerrabuildArgs.Run) |> target
    | p when p.Contains(TerrabuildArgs.Clear) -> p.GetResult(TerrabuildArgs.Clear) |> clear; 0
    | _ -> parser.PrintUsage() |> Terminal.writeLine; 0

[<EntryPoint>]
let main _ =
    try
        Terminal.hideCursor()
        Console.CancelKeyPress.Add (fun _ -> $"{Ansi.Emojis.bolt} Aborted{Ansi.Styles.cursorShow}" |> Terminal.writeLine)
        let ret = processCommandLine()

        Environment.CurrentDirectory <- launchDir
        Terminal.showCursor()
        ret
    with
        ex ->
            $"{Ansi.Emojis.bomb} Failed with error\n{ex}{Ansi.Styles.cursorShow}" |> Terminal.writeLine

            Environment.CurrentDirectory <- launchDir
            Terminal.showCursor()
            5
