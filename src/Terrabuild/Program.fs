open Argu
open CLI
open System
open Serilog
open Errors

let rec dumpKnownException (ex: Exception) =
    seq {
        match ex with
        | :? TerrabuildException as ex ->
            yield ex.Message
            yield! ex.InnerException |> dumpKnownException
        | null -> ()
        | _ ->
            yield! ex.InnerException |> dumpKnownException
    }

let rec dumpUnknownException (ex: Exception) =
    seq {
        match ex with
        | :? TerrabuildException as ex ->
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

let processCommandLine (parser: ArgumentParser<TerrabuildArgs>) (result: ParseResults<TerrabuildArgs>) =
    let debug = result.Contains(TerrabuildArgs.Debug)
    let whatIf = result.Contains(TerrabuildArgs.WhatIf)

    let logFile name = FS.combinePath launchDir $"terrabuild-debug.{name}"

    if debug then
        Log.Logger <-
            LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logFile "log")
                .CreateLogger()

    let runTarget wsDir target environment labels variables (options: Configuration.Options) =
        let wsDir = wsDir |> FS.fullPath
        Environment.CurrentDirectory <- wsDir

        if options.Debug then
            let jsonOptions = Json.Serialize options
            jsonOptions |> IO.writeTextFile (logFile "options.json")

        let sourceControl = SourceControls.Factory.create()
        let config = Configuration.read wsDir environment labels variables sourceControl options

        let token = config.Space |> Option.bind (fun space -> Cache.readAuthToken space)
        let api = Api.Factory.create config.Space token
        let storage = Storages.Factory.create api

        if options.Debug then
            let jsonConfig = Json.Serialize config
            jsonConfig |> IO.writeTextFile (logFile "config.json")

        let graph = Graph.buildGraph config target

        if options.Debug then
            let jsonGraph = Json.Serialize graph
            jsonGraph |> IO.writeTextFile (logFile "graph.json")
            let mermaid = Graph.graph graph |> String.join "\n"
            mermaid |> IO.writeTextFile (logFile "graph.mermaid")

        let cache = Cache.Cache(storage) :> Cache.ICache
        let buildGraph = Graph.optimize config graph cache options
        if options.Debug then
            let jsonBuildGraph = Json.Serialize buildGraph
            jsonBuildGraph |> IO.writeTextFile (logFile "buildgraph.json")
            let mermaid = Graph.graph buildGraph |> String.join "\n"
            mermaid |> IO.writeTextFile (logFile "buildgraph.mermaid")

        if options.WhatIf then 0
        else
            let buildNotification = Notification.BuildNotification() :> Build.IBuildNotification
            let build = Build.run config buildGraph cache api buildNotification options
            buildNotification.WaitCompletion()

            if options.Debug then
                let jsonBuild = Json.Serialize build
                jsonBuild |> IO.writeTextFile (logFile "build.json")

            if build.Status = Build.Status.Success then 0
            else 5


    let scaffold (scaffoldArgs: ParseResults<ScaffoldArgs>) =
        let wsDir = scaffoldArgs.GetResult(ScaffoldArgs.Workspace, defaultValue = ".")
        let force = scaffoldArgs.Contains(ScaffoldArgs.Force)
        Scalffold.scaffold wsDir force
        0

    let targetShortcut target (buildArgs: ParseResults<RunArgs>) =
        let wsDir = buildArgs.GetResult(RunArgs.Workspace, defaultValue = ".")
        let environment = buildArgs.TryGetResult(RunArgs.Environment) |> Option.defaultValue "default" |> String.toLower
        let labels = buildArgs.TryGetResult(RunArgs.Label) |> Option.map (fun labels -> labels |> Seq.map String.toLower |> Set)
        let variables = buildArgs.GetResults(RunArgs.Variable) |> Seq.map (fun (k, v) -> k |> String.toLower, v) |> Map
        let ``parallel`` = buildArgs.GetResult(RunArgs.Parallel, defaultValue = Environment.ProcessorCount/2) |> max 1
        let options = { Configuration.Options.WhatIf = whatIf
                        Configuration.Options.Debug = debug
                        Configuration.Options.Force = buildArgs.Contains(RunArgs.Force)
                        Configuration.Options.MaxConcurrency = ``parallel``
                        Configuration.Options.Retry = buildArgs.Contains(RunArgs.Retry)
                        Configuration.Options.StartedAt = DateTime.UtcNow }
        runTarget wsDir (Set.singleton target) environment labels variables options


    let target (targetArgs: ParseResults<TargetArgs>) =
        let targets = targetArgs.GetResult(TargetArgs.Target) |> Seq.map String.toLower
        let wsDir = targetArgs.GetResult(TargetArgs.Workspace, defaultValue = ".")
        let environment = targetArgs.TryGetResult(TargetArgs.Environment) |> Option.defaultValue "default" |> String.toLower
        let labels = targetArgs.TryGetResult(TargetArgs.Label) |> Option.map (fun labels -> labels |> Seq.map String.toLower |> Set)
        let variables = targetArgs.GetResults(TargetArgs.Variable) |> Seq.map (fun (k, v) -> k |> String.toLower, v) |> Map
        let ``parallel`` = targetArgs.GetResult(TargetArgs.Parallel, defaultValue = Environment.ProcessorCount/2) |> max 1
        let options = { Configuration.Options.WhatIf = whatIf
                        Configuration.Options.Debug = debug
                        Configuration.Options.Force = targetArgs.Contains(TargetArgs.Force)
                        Configuration.Options.MaxConcurrency = ``parallel``
                        Configuration.Options.Retry = targetArgs.Contains(TargetArgs.Retry)
                        Configuration.Options.StartedAt = DateTime.UtcNow }
        runTarget wsDir (Set targets) environment labels variables options


    let clear (clearArgs: ParseResults<ClearArgs>) =
        if clearArgs.Contains(ClearArgs.BuildCache) then Cache.clearBuildCache()

    let login (loginArgs: ParseResults<LoginArgs>) =
        let space = loginArgs.GetResult(LoginArgs.Space)
        let token = loginArgs.GetResult(LoginArgs.Token)
        Auth.login space token

    let logout (logoutArgs: ParseResults<LogoutArgs>)=
        let space = logoutArgs.GetResult(LogoutArgs.Space)
        Auth.logout space

    match result.GetAllResults() with
    | TerrabuildArgs.Scaffold arg :: _ -> scaffold arg
    | TerrabuildArgs.Build arg :: _ -> targetShortcut "build" arg
    | TerrabuildArgs.Test arg :: _ -> targetShortcut "test" arg
    | TerrabuildArgs.Dist arg :: _ -> targetShortcut "dist" arg
    | TerrabuildArgs.Publish arg :: _ -> targetShortcut "publish" arg
    | TerrabuildArgs.Deploy arg :: _ -> targetShortcut "deploy" arg
    | TerrabuildArgs.Serve arg :: _ -> targetShortcut "serve" arg
    | TerrabuildArgs.Run arg :: _ -> target arg
    | TerrabuildArgs.Clear arg :: _ -> clear arg; 0
    | TerrabuildArgs.Login arg :: _ ->  login arg; 0
    | TerrabuildArgs.Logout arg :: _ ->  logout arg; 0
    | _ -> parser.PrintUsage() |> Terminal.writeLine; 0

[<EntryPoint>]
let main _ =
    let mutable debug = false
    let retCode =
        try
            DotNetEnv.Env.TraversePath().Load() |> ignore
            Terminal.hideCursor()
            Console.CancelKeyPress.Add (fun _ -> $"{Ansi.Emojis.bolt} Aborted{Ansi.Styles.cursorShow}" |> Terminal.writeLine)
            let errorHandler = TerrabuildExiter()
            let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild", errorHandler = errorHandler)
            let result = parser.ParseCommandLine()
            debug <- result.Contains(TerrabuildArgs.Debug)
            processCommandLine parser result
        with
            | :? TerrabuildException as ex ->
                Log.Fatal("Failed with {Exception}", ex)
                let reason =
                    if debug then ex.ToString()
                    else dumpUnknownException ex |> String.join "\n   "
                $"{Ansi.Emojis.explosion} {reason}" |> Terminal.writeLine
                5
            | ex ->
                let reason = ex.ToString()
                $"{Ansi.Emojis.explosion} {reason}" |> Terminal.writeLine
                5

    Environment.CurrentDirectory <- launchDir
    Terminal.showCursor()
    retCode
