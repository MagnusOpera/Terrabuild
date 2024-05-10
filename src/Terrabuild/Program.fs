open Argu
open CLI
open System
open Serilog
open Errors
open Terrabuild.Expressions

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
    let whatIf = result.Contains(TerrabuildArgs.WhatIf)

    let logFile name = FS.combinePath launchDir $"terrabuild-debug.{name}"

    if debug then
        Log.Logger <-
            LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logFile "log")
                .CreateLogger()
        Log.Debug("Log created")

    let runTarget wsDir target environment labels variables (options: Configuration.Options) =
        let wsDir = wsDir |> FS.fullPath
        Environment.CurrentDirectory <- wsDir
        Log.Debug("Changing current directory to {directory}", wsDir)

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
        let wsDir =
            match buildArgs.TryGetResult(RunArgs.Workspace) with
            | Some ws -> ws
            | _ ->
                match Environment.CurrentDirectory |> findWorkspace with
                | Some ws -> ws
                | _ -> TerrabuildException.Raise "Can't find workspace root directory. Check you are in a workspace."
        let environment = buildArgs.TryGetResult(RunArgs.Environment) |> Option.defaultValue "default" |> String.toLower
        let labels = buildArgs.TryGetResult(RunArgs.Label) |> Option.map (fun labels -> labels |> Seq.map String.toLower |> Set)
        let variables = buildArgs.GetResults(RunArgs.Variable) |> Seq.map (fun (k, v) -> k, v) |> Map
        let maxConcurrency = buildArgs.GetResult(RunArgs.Parallel, defaultValue = Environment.ProcessorCount/2) |> max 1
        let options = { Configuration.Options.WhatIf = whatIf
                        Configuration.Options.Debug = debug
                        Configuration.Options.Force = buildArgs.Contains(RunArgs.Force)
                        Configuration.Options.MaxConcurrency = maxConcurrency
                        Configuration.Options.Retry = buildArgs.Contains(RunArgs.Retry)
                        Configuration.Options.StartedAt = DateTime.UtcNow }
        runTarget wsDir (Set.singleton target) environment labels variables options

    let target (targetArgs: ParseResults<TargetArgs>) =
        let targets = targetArgs.GetResult(TargetArgs.Target) |> Seq.map String.toLower
        let wsDir =
            match targetArgs.TryGetResult(TargetArgs.Workspace) with
            | Some ws -> ws
            | _ ->
                match Environment.CurrentDirectory |> findWorkspace with
                | Some ws -> ws
                | _ -> TerrabuildException.Raise "Can't find workspace root directory. Check you are in a workspace."
        let environment = targetArgs.TryGetResult(TargetArgs.Environment) |> Option.defaultValue "default" |> String.toLower
        let labels = targetArgs.TryGetResult(TargetArgs.Label) |> Option.map (fun labels -> labels |> Seq.map String.toLower |> Set)
        let variables = targetArgs.GetResults(TargetArgs.Variable) |> Seq.map (fun (k, v) -> k, v) |> Map
        let maxConcurrency = targetArgs.GetResult(TargetArgs.Parallel, defaultValue = Environment.ProcessorCount/2) |> max 1
        let options = { Configuration.Options.WhatIf = whatIf
                        Configuration.Options.Debug = debug
                        Configuration.Options.Force = targetArgs.Contains(TargetArgs.Force)
                        Configuration.Options.MaxConcurrency = maxConcurrency
                        Configuration.Options.Retry = targetArgs.Contains(TargetArgs.Retry)
                        Configuration.Options.StartedAt = DateTime.UtcNow }
        runTarget wsDir (Set targets) environment labels variables options


    let clear (clearArgs: ParseResults<ClearArgs>) =
        if clearArgs.Contains(ClearArgs.Cache) then Cache.clearBuildCache()
        if clearArgs.Contains(ClearArgs.Home) then Cache.clearHomeCache()
        0

    let login (loginArgs: ParseResults<LoginArgs>) =
        let space = loginArgs.GetResult(LoginArgs.Space)
        let token = loginArgs.GetResult(LoginArgs.Token)
        Auth.login space token
        0

    let logout (logoutArgs: ParseResults<LogoutArgs>)=
        let space = logoutArgs.GetResult(LogoutArgs.Space)
        Auth.logout space
        0

    Log.Debug("Parsing command line")
    match result with
    | p when p.Contains(TerrabuildArgs.Scaffold) -> p.GetResult(TerrabuildArgs.Scaffold) |> scaffold
    | p when p.Contains(TerrabuildArgs.Build) -> p.GetResult(TerrabuildArgs.Build) |> targetShortcut "build"
    | p when p.Contains(TerrabuildArgs.Test) -> p.GetResult(TerrabuildArgs.Test) |> targetShortcut "test"
    | p when p.Contains(TerrabuildArgs.Dist) -> p.GetResult(TerrabuildArgs.Dist) |> targetShortcut "dist"
    | p when p.Contains(TerrabuildArgs.Publish) -> p.GetResult(TerrabuildArgs.Publish) |> targetShortcut "publish"
    | p when p.Contains(TerrabuildArgs.Deploy) -> p.GetResult(TerrabuildArgs.Deploy) |> targetShortcut "deploy"
    | p when p.Contains(TerrabuildArgs.Serve) -> p.GetResult(TerrabuildArgs.Serve) |> targetShortcut "serve"
    | p when p.Contains(TerrabuildArgs.Run) -> p.GetResult(TerrabuildArgs.Run) |> target
    | p when p.Contains(TerrabuildArgs.Clear) -> p.GetResult(TerrabuildArgs.Clear) |> clear
    | p when p.Contains(TerrabuildArgs.Login) -> p.GetResult(TerrabuildArgs.Login) |> login
    | p when p.Contains(TerrabuildArgs.Logout) -> p.GetResult(TerrabuildArgs.Logout) |> logout
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
                Log.Fatal("Failed with {Exception}", ex)
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
