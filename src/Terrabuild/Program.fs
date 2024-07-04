open Argu
open CLI
open System
open Serilog
open Errors
open System.Reflection

let rec dumpKnownException (ex: Exception) =
    seq {
        match ex with
        | :? TerrabuildException as ex ->
            yield ex.Message
            yield! ex.InnerException |> dumpKnownException
        | null -> ()
        | _ -> ()
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

let rec findWorkspace dir =
    if FS.combinePath dir "WORKSPACE" |> IO.exists then
        Some dir
    else
        match FS.parentDirectory dir with
        | null -> None
        | parentDir -> findWorkspace parentDir

let processCommandLine (parser: ArgumentParser<TerrabuildArgs>) (result: ParseResults<TerrabuildArgs>) =
    let debug = result.Contains(TerrabuildArgs.Debug)
    let runId = Guid.NewGuid()

    let logFile name = FS.combinePath launchDir $"terrabuild-debug.{name}"

    if debug then
        Log.Logger <-
            LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logFile "log")
                .CreateLogger()
        Log.Debug("Log created")

    let runTarget wsDir configuration note tag labels variables logs (options: Configuration.Options) =
        let logGraph graph name =
            graph
            |> Json.Serialize
            |> IO.writeTextFile (logFile $"{name}-graph.json")
            graph
            |> GraphDef.render
            |> String.join "\n"
            |> IO.writeTextFile (logFile $"{name}-graph.mermaid")

        let wsDir = wsDir |> FS.fullPath
        Environment.CurrentDirectory <- wsDir
        Log.Debug("Changing current directory to {directory}", wsDir)
        Log.Debug("ProcessorCount = {procCount}", Environment.ProcessorCount)

        // create temporary folder so extensions can expose files to docker containers (folder must within workspaceDir hierarchy)
        IO.createDirectory ".terrabuild"

        if options.Debug then
            let jsonOptions = Json.Serialize options
            jsonOptions |> IO.writeTextFile (logFile "options.json")

        let sourceControl = SourceControls.Factory.create()
        let config = Configuration.read wsDir configuration note tag labels variables sourceControl options

        let token =
            if options.LocalOnly then None
            else config.Space |> Option.bind (fun space -> Auth.readAuthToken space)
        let api = Api.Factory.create config.Space token
        if api |> Option.isSome then
            Log.Debug("Connected to API")
            $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} Connected to Insights" |> Terminal.writeLine

        if options.Debug then
            let jsonConfig = Json.Serialize config
            jsonConfig |> IO.writeTextFile (logFile "config.json")

        let storage = Storages.Factory.create api
        let cache = Cache.Cache(storage) :> Cache.ICache

        let buildGraph =
            let graph = GraphAnalysisBuilder.build options config
            if options.Debug then logGraph graph "config"

            let consistentGraph = GraphAnalysisConsistency.enforce options cache graph
            if options.Debug then logGraph consistentGraph "consistent"

            let transformGraph = GraphTransformBuilder.build consistentGraph
            if options.Debug then logGraph transformGraph "transform"

            let optimizeGraph =
                if options.NoBatch then transformGraph
                else GraphTransformOptimizer.optimize options sourceControl transformGraph
            if options.Debug then logGraph optimizeGraph "optimize"

            let nodesToRun = graph.Nodes.Count
            $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} {nodesToRun} tasks" |> Terminal.writeLine
            optimizeGraph

        if options.Debug then logGraph buildGraph "build"

        if options.WhatIf then
            if logs then
                Logs.dumpLogs runId options cache sourceControl None buildGraph 
            0
        else
            let buildNotification = Notification.BuildNotification() :> Build.IBuildNotification
            let summary = Build.run options sourceControl config cache api buildNotification buildGraph
            buildNotification.WaitCompletion()

            if options.Debug then
                let jsonBuild = Json.Serialize summary
                jsonBuild |> IO.writeTextFile (logFile "build-result.json")

            if logs || summary.Status <> Build.Status.Success then
                Logs.dumpLogs runId options cache sourceControl (Some summary.BuildNodes) buildGraph  

            let result =
                match summary.Status with
                | Build.Status.Success -> Ansi.Emojis.happy
                | _ -> Ansi.Emojis.sad

            $"{result} Completed in {summary.TotalDuration}" |> Terminal.writeLine
            if summary.Status = Build.Status.Success then 0
            else 5

    let scaffold (scaffoldArgs: ParseResults<ScaffoldArgs>) =
        let wsDir = scaffoldArgs.GetResult(ScaffoldArgs.Workspace, defaultValue = ".")
        let force = scaffoldArgs.Contains(ScaffoldArgs.Force)
        Scalffold.scaffold wsDir force
        0

    let run (runArgs: ParseResults<RunArgs>) =
        let targets = runArgs.GetResult(RunArgs.Target) |> Seq.map String.toLower
        let wsDir =
            match runArgs.TryGetResult(RunArgs.Workspace) with
            | Some ws -> ws
            | _ ->
                match Environment.CurrentDirectory |> findWorkspace with
                | Some ws -> ws
                | _ -> TerrabuildException.Raise("Can't find workspace root directory. Check you are in a workspace.")
        let configuration = runArgs.TryGetResult(RunArgs.Configuration) |> Option.defaultValue "default" |> String.toLower
        let note = runArgs.TryGetResult(RunArgs.Note)
        let labels = runArgs.TryGetResult(RunArgs.Label) |> Option.map (fun labels -> labels |> Seq.map String.toLower |> Set)
        let variables = runArgs.GetResults(RunArgs.Variable) |> Seq.map (fun (k, v) -> k, v) |> Map
        let maxConcurrency = runArgs.GetResult(RunArgs.Parallel, defaultValue = Environment.ProcessorCount/2) |> max 1
        let noContainer = runArgs.Contains(RunArgs.NoContainer)
        let noBatch = runArgs.Contains(RunArgs.NoBatch)
        let localOnly = runArgs.Contains(RunArgs.LocalOnly)
        let logs = runArgs.Contains(RunArgs.Logs)
        let tag = runArgs.TryGetResult(RunArgs.Tag)
        let whatIf = runArgs.Contains(RunArgs.WhatIf)
        let options = { Configuration.Options.WhatIf = whatIf
                        Configuration.Options.Debug = debug
                        Configuration.Options.Force = runArgs.Contains(RunArgs.Force)
                        Configuration.Options.MaxConcurrency = maxConcurrency
                        Configuration.Options.NoContainer = noContainer
                        Configuration.Options.NoBatch = noBatch
                        Configuration.Options.Retry = runArgs.Contains(RunArgs.Retry)
                        Configuration.Options.StartedAt = DateTime.UtcNow
                        Configuration.Options.IsLog = false
                        Configuration.Options.Targets = Set targets
                        Configuration.Options.LocalOnly = localOnly }
        runTarget wsDir configuration note tag labels variables logs options

    let logs (logsArgs: ParseResults<LogsArgs>) =
        let targets = logsArgs.GetResult(LogsArgs.Target) |> Seq.map String.toLower
        let wsDir =
            match logsArgs.TryGetResult(LogsArgs.Workspace) with
            | Some ws -> ws
            | _ ->
                match Environment.CurrentDirectory |> findWorkspace with
                | Some ws -> ws
                | _ -> TerrabuildException.Raise("Can't find workspace root directory. Check you are in a workspace.")
        let configuration = logsArgs.TryGetResult(LogsArgs.Configuration) |> Option.defaultValue "default" |> String.toLower
        let labels = logsArgs.TryGetResult(LogsArgs.Label) |> Option.map (fun labels -> labels |> Seq.map String.toLower |> Set)
        let variables = logsArgs.GetResults(LogsArgs.Variable) |> Seq.map (fun (k, v) -> k, v) |> Map
        let options = { Configuration.Options.WhatIf = true
                        Configuration.Options.Debug = debug
                        Configuration.Options.Force = false
                        Configuration.Options.MaxConcurrency = 1
                        Configuration.Options.NoContainer = false
                        Configuration.Options.NoBatch = true
                        Configuration.Options.Retry = false
                        Configuration.Options.StartedAt = DateTime.UtcNow
                        Configuration.Options.IsLog = true
                        Configuration.Options.Targets = Set targets
                        Configuration.Options.LocalOnly = true }
        runTarget wsDir configuration None None labels variables true options

    let clear (clearArgs: ParseResults<ClearArgs>) =
        if clearArgs.Contains(ClearArgs.Cache) || clearArgs.Contains(ClearArgs.All) then Cache.clearBuildCache()
        if clearArgs.Contains(ClearArgs.Home) || clearArgs.Contains(ClearArgs.All) then Cache.clearHomeCache()
        0

    let login (loginArgs: ParseResults<LoginArgs>) =
        let space = loginArgs.GetResult(LoginArgs.Space)
        let token = loginArgs.GetResult(LoginArgs.Token)
        Auth.login space token
        0

    let logout (logoutArgs: ParseResults<LogoutArgs>) =
        let space = logoutArgs.GetResult(LogoutArgs.Space)
        Auth.logout space
        0

    let version () =
        let version =
            Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    .InformationalVersion
        printfn $"Terrabuild v{version}"
        0
 
    Log.Debug("Parsing command line")
    match result with
    | p when p.Contains(TerrabuildArgs.Scaffold) -> p.GetResult(TerrabuildArgs.Scaffold) |> scaffold
    | p when p.Contains(TerrabuildArgs.Logs) -> p.GetResult(TerrabuildArgs.Logs) |> logs
    | p when p.Contains(TerrabuildArgs.Run) -> p.GetResult(TerrabuildArgs.Run) |> run
    | p when p.Contains(TerrabuildArgs.Clear) -> p.GetResult(TerrabuildArgs.Clear) |> clear
    | p when p.Contains(TerrabuildArgs.Login) -> p.GetResult(TerrabuildArgs.Login) |> login
    | p when p.Contains(TerrabuildArgs.Logout) -> p.GetResult(TerrabuildArgs.Logout) |> logout
    | p when p.Contains(TerrabuildArgs.Version) -> version()
    | _ ->
        Log.Debug("Failed to parse {result}", result)
        parser.PrintUsage() |> Terminal.writeLine; 0

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
                Log.Fatal("Failed with {Exception}", ex.ToString())
                let reason =
                    if debug then ex.ToString()
                    else dumpKnownException ex |> String.join "\n   "
                $"{Ansi.Emojis.explosion} {reason}" |> Terminal.writeLine
                5
            | ex ->
                Log.Fatal("Failed with {Exception}", ex)
                $"{Ansi.Emojis.explosion} {ex}" |> Terminal.writeLine
                5

    Environment.CurrentDirectory <- launchDir
    Terminal.showCursor()
    retCode
