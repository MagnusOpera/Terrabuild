open Argu
open CLI
open System
open Serilog
open Errors
open System.Reflection
open Collections


[<RequireQualifiedAccess>]
type RunTargetOptions = {
    Workspace: string
    WhatIf: bool
    Debug: bool
    MaxConcurrency: int
    Force: bool
    Retry: bool
    LocalOnly: bool
    CheckState: bool
    StartedAt: DateTime
    IsLog: bool
    Targets: string set
    Configuration: string
    Note: string option
    Tag: string option
    Labels: string set option
    Variables: Map<string, string>
    ContainerTool: string option
}


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
        Log.Debug("===== [Execution Start] =====")

    let runTarget logs (options: RunTargetOptions) =
        let logGraph graph name =
            graph
            |> Json.Serialize
            |> IO.writeTextFile (logFile $"{name}-graph.json")
            graph
            |> Mermaid.render None None
            |> String.join "\n"
            |> IO.writeTextFile (logFile $"{name}-graph.mermaid")

        Environment.CurrentDirectory <- options.Workspace
        Log.Debug("Changing current directory to {directory}", options.Workspace)
        Log.Debug("ProcessorCount = {procCount}", Environment.ProcessorCount)

        let sourceControl = SourceControls.Factory.create()

        let options = {
            ConfigOptions.Options.Workspace = options.Workspace
            ConfigOptions.Options.WhatIf = options.WhatIf
            ConfigOptions.Options.Debug = options.Debug
            ConfigOptions.Options.MaxConcurrency = options.MaxConcurrency
            ConfigOptions.Options.Force = options.Force
            ConfigOptions.Options.Retry = options.Retry
            ConfigOptions.Options.LocalOnly = options.LocalOnly
            ConfigOptions.Options.CheckState = options.CheckState
            ConfigOptions.Options.StartedAt = options.StartedAt
            ConfigOptions.Options.Targets = options.Targets
            ConfigOptions.Options.CI = sourceControl.CI
            ConfigOptions.Options.BranchOrTag = sourceControl.BranchOrTag
            ConfigOptions.Options.HeadCommit = sourceControl.HeadCommit
            ConfigOptions.Options.Metadata = sourceControl.Metadata
            ConfigOptions.Options.LogType = sourceControl.LogType
            ConfigOptions.Options.Configuration = options.Configuration
            ConfigOptions.Options.Note = options.Note
            ConfigOptions.Options.Tag = options.Tag
            ConfigOptions.Options.Labels = options.Labels
            ConfigOptions.Options.Variables = options.Variables
            ConfigOptions.Options.ContainerTool = options.ContainerTool
        }

        if options.Debug then
            let jsonOptions = Json.Serialize options
            jsonOptions |> IO.writeTextFile (logFile "options.json")

        let config = Configuration.read options

        let token =
            if options.LocalOnly then None
            else config.Space |> Option.bind Auth.readAuthToken
        let api = Api.Factory.create config.Space token options
        if api |> Option.isSome then
            Log.Debug("Connected to API")
            $" {Ansi.Styles.green}{Ansi.Emojis.checkmark}{Ansi.Styles.reset} Connected to Insights" |> Terminal.writeLine

        if options.Debug then
            let jsonConfig = Json.Serialize config
            jsonConfig |> IO.writeTextFile (logFile "config.json")

        let storage = Storages.Factory.create api
        let cache = Cache.Cache(storage) :> Cache.ICache

        let buildGraph = GraphBuilder.build options config
        if options.Debug then logGraph buildGraph "build"

        if not options.WhatIf then
            let buildNotification = Notification.BuildNotification() :> Build.IBuildNotification

            let summary = Build.run options cache api buildNotification buildGraph
            buildNotification.WaitCompletion()
            api |> Option.iter (fun api -> api.CompleteBuild summary.IsSuccess)

            if options.Debug then
                let jsonBuild = Json.Serialize summary
                jsonBuild |> IO.writeTextFile (logFile "build-result.json")

            if logs || not summary.IsSuccess then
                Logs.dumpLogs runId options cache buildGraph summary

            let result =
                if summary.IsSuccess then Ansi.Emojis.happy
                else Ansi.Emojis.sad

            $"{result} Completed in {summary.TotalDuration}" |> Terminal.writeLine
            if summary.IsSuccess then 0
            else 5
        else
            0

    let scaffold (scaffoldArgs: ParseResults<ScaffoldArgs>) =
        let wsDir = scaffoldArgs.GetResult(ScaffoldArgs.Workspace, defaultValue = ".")
        let force = scaffoldArgs.Contains(ScaffoldArgs.Force)
        Scalffold.scaffold wsDir force
        0

    let run (runArgs: ParseResults<RunArgs>) =
        let wsDir =
            match runArgs.TryGetResult(RunArgs.Workspace) with
            | Some ws -> ws
            | _ ->
                match Environment.CurrentDirectory |> findWorkspace with
                | Some ws -> ws
                | _ -> TerrabuildException.Raise("Can't find workspace root directory. Check you are in a workspace.")
        let targets = runArgs.GetResult(RunArgs.Target) |> Seq.map String.toLower
        let configuration = runArgs.TryGetResult(RunArgs.Configuration) |> Option.defaultValue "default" |> String.toLower
        let note = runArgs.TryGetResult(RunArgs.Note)
        let labels = runArgs.TryGetResult(RunArgs.Label) |> Option.map (fun labels -> labels |> Seq.map String.toLower |> Set)
        let variables = runArgs.GetResults(RunArgs.Variable) |> Map
        let maxConcurrency = runArgs.GetResult(RunArgs.Parallel, defaultValue = Environment.ProcessorCount/2) |> max 1
        let localOnly = runArgs.Contains(RunArgs.Local_Only)
        let checkState = runArgs.Contains(RunArgs.Check_State)
        let logs = runArgs.Contains(RunArgs.Logs)
        let tag = runArgs.TryGetResult(RunArgs.Tag)
        let whatIf = runArgs.Contains(RunArgs.WhatIf)
        let containerTool =
            match runArgs.TryGetResult(RunArgs.Container_Tool) with
            | Some ContainerTool.Docker -> Some "docker"
            | Some ContainerTool.Podman -> Some "podman"
            | Some ContainerTool.None -> None
            | _ -> Some "docker"

        let options = { RunTargetOptions.Workspace = wsDir |> FS.fullPath
                        RunTargetOptions.WhatIf = whatIf
                        RunTargetOptions.Debug = debug
                        RunTargetOptions.Force = runArgs.Contains(RunArgs.Force)
                        RunTargetOptions.MaxConcurrency = maxConcurrency
                        RunTargetOptions.Retry = runArgs.Contains(RunArgs.Retry)
                        RunTargetOptions.StartedAt = DateTime.UtcNow
                        RunTargetOptions.IsLog = false
                        RunTargetOptions.Targets = Set targets
                        RunTargetOptions.LocalOnly = localOnly
                        RunTargetOptions.CheckState = checkState
                        RunTargetOptions.Configuration = configuration
                        RunTargetOptions.Note = note
                        RunTargetOptions.Tag = tag
                        RunTargetOptions.Labels = labels
                        RunTargetOptions.Variables = variables
                        RunTargetOptions.ContainerTool = containerTool }
        runTarget logs options

    let serve (serveArgs: ParseResults<ServeArgs>) =
        let wsDir =
            match serveArgs.TryGetResult(ServeArgs.Workspace) with
            | Some ws -> ws
            | _ ->
                match Environment.CurrentDirectory |> findWorkspace with
                | Some ws -> ws
                | _ -> TerrabuildException.Raise("Can't find workspace root directory. Check you are in a workspace.")
        let configuration = serveArgs.TryGetResult(ServeArgs.Configuration) |> Option.defaultValue "default" |> String.toLower
        let labels = serveArgs.TryGetResult(ServeArgs.Label) |> Option.map (fun labels -> labels |> Seq.map String.toLower |> Set)
        let variables = serveArgs.GetResults(ServeArgs.Variable) |> Map
        let options = { RunTargetOptions.Workspace = wsDir |> FS.fullPath
                        RunTargetOptions.WhatIf = false
                        RunTargetOptions.Debug = debug
                        RunTargetOptions.Force = false
                        RunTargetOptions.MaxConcurrency = Int32.MaxValue
                        RunTargetOptions.Retry = true
                        RunTargetOptions.StartedAt = DateTime.UtcNow
                        RunTargetOptions.IsLog = false
                        RunTargetOptions.Targets = Set [ "serve" ]
                        RunTargetOptions.LocalOnly = true
                        RunTargetOptions.CheckState = false
                        RunTargetOptions.Configuration = configuration
                        RunTargetOptions.Note = None
                        RunTargetOptions.Tag = None
                        RunTargetOptions.Labels = labels
                        RunTargetOptions.Variables = variables
                        RunTargetOptions.ContainerTool = None }
        runTarget true options

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
        let variables = logsArgs.GetResults(LogsArgs.Variable) |> Map

        let options = { RunTargetOptions.Workspace = wsDir |> FS.fullPath
                        RunTargetOptions.WhatIf = true
                        RunTargetOptions.Debug = debug
                        RunTargetOptions.Force = false
                        RunTargetOptions.MaxConcurrency = 1
                        RunTargetOptions.Retry = false
                        RunTargetOptions.StartedAt = DateTime.UtcNow
                        RunTargetOptions.IsLog = true
                        RunTargetOptions.Targets = Set targets
                        RunTargetOptions.LocalOnly = true 
                        RunTargetOptions.CheckState = false
                        RunTargetOptions.Configuration = configuration
                        RunTargetOptions.Note = None
                        RunTargetOptions.Tag = None
                        RunTargetOptions.Labels = labels
                        RunTargetOptions.Variables = variables
                        RunTargetOptions.ContainerTool = None }
        runTarget true options

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
    | p when p.Contains(TerrabuildArgs.Serve) -> p.GetResult(TerrabuildArgs.Serve) |> serve
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
    Log.Debug("===== [Execution End] =====")
    retCode
