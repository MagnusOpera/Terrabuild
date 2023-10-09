open Argu
open System
open CLI

let runTarget wsDir target shared options =
    Console.WriteLine($"{Ansi.Emojis.box} Reading configuration")
    let config = Configuration.read wsDir shared
    Console.WriteLine($"{Ansi.Emojis.popcorn} Constructing graph")
    let graph = Graph.buildGraph config target
    let cache = Cache.Cache(config.Storage)
    let buildNotification = Notification.BuildNotification() :> Build.IBuildNotification
    Build.run config graph cache buildNotification options
    buildNotification.WaitCompletion()


let targetShortcut target (buildArgs: ParseResults<RunArgs>) =
    let wsDir = buildArgs.GetResult(RunArgs.Workspace, defaultValue = ".")
    let shared = buildArgs.TryGetResult(RunArgs.Shared) |> Option.isSome
    let options = { Build.BuildOptions.NoCache = buildArgs.Contains(RunArgs.NoCache)
                    Build.BuildOptions.MaxConcurrency = buildArgs.GetResult(RunArgs.Parallel, defaultValue = 4)
                    Build.BuildOptions.Retry = buildArgs.Contains(RunArgs.Retry) }
    runTarget wsDir target shared options


let target (targetArgs: ParseResults<TargetArgs>) =
    let target = targetArgs.GetResult(TargetArgs.Target)
    let wsDir = targetArgs.GetResult(TargetArgs.Workspace, defaultValue = ".")
    let shared = targetArgs.TryGetResult(TargetArgs.Shared) |> Option.isSome
    let options = { Build.BuildOptions.NoCache = targetArgs.Contains(TargetArgs.NoCache)
                    Build.BuildOptions.MaxConcurrency = targetArgs.GetResult(TargetArgs.Parallel, defaultValue = 4)
                    Build.BuildOptions.Retry = targetArgs.Contains(TargetArgs.Retry) }
    runTarget wsDir target shared options


let clear (clearArgs: ParseResults<ClearArgs>) =
    if clearArgs.Contains(ClearArgs.BuildCache) then Cache.clearBuildCache()

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
    match parser.ParseCommandLine() with
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
