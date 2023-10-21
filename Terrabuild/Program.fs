open Argu
open CLI
open System

let rec dumpKnownException (ex: Exception) =
    seq {
        match ex with
        | null -> ()
        | :? Configuration.ConfigException as ex ->
            yield ex.Message
            yield! ex.InnerException |> dumpKnownException 
        | _ -> yield ex.ToString()
    }


type TerrabuildExiter() =
    interface IExiter with
        member _.Name: string = "Process Exiter"

        member _.Exit(msg: string, errorCode: ErrorCode) =
            let writer = if errorCode = ErrorCode.HelpText then Console.Out else Console.Error
            do
                writer.WriteLine msg
                writer.Flush()
                Console.Write(Ansi.Styles.cursorShow)
                Console.Out.Flush()

            exit (int errorCode)

let processCommandLine () =
    let errorHandler = TerrabuildExiter()
    let parser = ArgumentParser.Create<CLI.TerrabuildArgs>(programName = "terrabuild", errorHandler = errorHandler)
    let result = parser.ParseCommandLine()
    let debug = result.Contains(TerrabuildArgs.Debug)

    let runTarget wsDir target shared environment labels variables options =
        try
            Console.WriteLine($"{Ansi.Emojis.box} Reading configuration")
            let config = Configuration.read wsDir shared environment labels variables

            if debug then
                let jsonConfig = Json.Serialize config
                jsonConfig |> IO.writeTextFile "terrabuild.config.json"

            Console.WriteLine($"{Ansi.Emojis.popcorn} Constructing graph for {config.Environment}")
            let graph = Graph.buildGraph config target

            if debug then
                let jsonGraph = Json.Serialize graph
                jsonGraph |> IO.writeTextFile "terrabuild.graph.json"

            let cache = Cache.Cache(config.Storage)
            let buildNotification = Notification.BuildNotification() :> Build.IBuildNotification
            let build = Build.run config graph cache buildNotification options
            buildNotification.WaitCompletion()

            if debug then
                let jsonBuild = Json.Serialize build
                jsonBuild |> IO.writeTextFile "terrabuild.build.json"

        with
            | :? Configuration.ConfigException as ex ->
                let reason = dumpKnownException ex |> Seq.rev |> String.join ", "
                Console.WriteLine($"{Ansi.Emojis.explosion} {reason}")

    let targetShortcut target (buildArgs: ParseResults<RunArgs>) =
        let wsDir = buildArgs.GetResult(RunArgs.Workspace, defaultValue = ".")
        let shared = buildArgs.TryGetResult(RunArgs.Shared) |> Option.isSome
        let environment = buildArgs.TryGetResult(RunArgs.Environment) |> Option.defaultValue "default"
        let labels = buildArgs.TryGetResult(RunArgs.Label) |> Option.map Set
        let variables = buildArgs.GetResults(RunArgs.Variable) |> Map
        let options = { Build.BuildOptions.NoCache = buildArgs.Contains(RunArgs.NoCache)
                        Build.BuildOptions.MaxConcurrency = buildArgs.GetResult(RunArgs.Parallel, defaultValue = Environment.ProcessorCount)
                        Build.BuildOptions.Retry = buildArgs.Contains(RunArgs.Retry) }
        runTarget wsDir [target] shared environment labels variables options


    let target (targetArgs: ParseResults<TargetArgs>) =
        let targets = targetArgs.GetResult(TargetArgs.Target)
        let wsDir = targetArgs.GetResult(TargetArgs.Workspace, defaultValue = ".")
        let shared = targetArgs.TryGetResult(TargetArgs.Shared) |> Option.isSome
        let environment = targetArgs.TryGetResult(TargetArgs.Environment) |> Option.defaultValue "default"
        let labels = targetArgs.TryGetResult(TargetArgs.Label) |> Option.map Set
        let variables = targetArgs.GetResults(TargetArgs.Variable) |> Map
        let options = { Build.BuildOptions.NoCache = targetArgs.Contains(TargetArgs.NoCache)
                        Build.BuildOptions.MaxConcurrency = targetArgs.GetResult(TargetArgs.Parallel, defaultValue = Environment.ProcessorCount)
                        Build.BuildOptions.Retry = targetArgs.Contains(TargetArgs.Retry) }
        runTarget wsDir targets shared environment labels variables options


    let clear (clearArgs: ParseResults<ClearArgs>) =
        if clearArgs.Contains(ClearArgs.BuildCache) then Cache.clearBuildCache()

    match result with
    | p when p.Contains(TerrabuildArgs.Build) -> p.GetResult(TerrabuildArgs.Build) |> targetShortcut "build"
    | p when p.Contains(TerrabuildArgs.Test) -> p.GetResult(TerrabuildArgs.Test) |> targetShortcut "test"
    | p when p.Contains(TerrabuildArgs.Dist) -> p.GetResult(TerrabuildArgs.Dist) |> targetShortcut "dist"
    | p when p.Contains(TerrabuildArgs.Serve) -> p.GetResult(TerrabuildArgs.Serve) |> targetShortcut "serve"
    | p when p.Contains(TerrabuildArgs.Run) -> p.GetResult(TerrabuildArgs.Run) |> target
    | p when p.Contains(TerrabuildArgs.Clear) -> p.GetResult(TerrabuildArgs.Clear) |> clear
    | _ -> printfn $"{parser.PrintUsage()}"

[<EntryPoint>]
let main _ =
    try
        Console.Write(Ansi.Styles.cursorHide)
        Console.Out.Flush()

        Console.CancelKeyPress.Add (fun _ ->
            Console.WriteLine($"{Ansi.Emojis.bolt} Aborted{Ansi.Styles.cursorShow}")
            Console.Out.Flush())
        processCommandLine()

        Console.Write(Ansi.Styles.cursorShow)
        Console.Out.Flush()
        0
    with
        ex ->
            Console.WriteLine($"{Ansi.Emojis.bomb} Failed with error\n{ex}{Ansi.Styles.cursorShow}")
            Console.Out.Flush()
            5
