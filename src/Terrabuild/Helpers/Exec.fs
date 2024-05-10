module Exec
open System.Diagnostics
open System
open System.IO
open System.Collections.Generic
open Errors

type CaptureResult =
    | Success of string*int
    | Error of string*int

let private createProcess workingDir command args =
    let psi = ProcessStartInfo (FileName = command,
                                Arguments = args,
                                UseShellExecute = false,
                                WorkingDirectory = workingDir,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true)
    new Process(StartInfo = psi)

let execCaptureOutput (workingDir: string) (command: string) (args: string) =
    use proc = createProcess workingDir command args
    proc.Start() |> ignore
    proc.WaitForExit()

    match proc.ExitCode with
    | 0 -> Success (proc.StandardOutput.ReadToEnd(), proc.ExitCode)
    | _ -> Error (proc.StandardError.ReadToEnd(), proc.ExitCode)

let execCaptureTimestampedOutput (workingDir: string) (command: string) (args: string) (logFile: string) =
    try
        use logWriter = new StreamWriter(logFile)
        let writeLock = obj()

        let inline lockWrite (from: string) (msg: string) =
            lock writeLock (fun () -> logWriter.WriteLine($"{DateTime.UtcNow} {from} {msg}"))

        use proc = createProcess workingDir command args
        proc.OutputDataReceived.Add(fun e -> lockWrite "OUT" e.Data)
        proc.ErrorDataReceived.Add(fun e -> lockWrite "ERR" e.Data)
        proc.Start() |> ignore
        proc.BeginOutputReadLine()
        proc.BeginErrorReadLine()
        proc.WaitForExit()

        proc.ExitCode
    with
    | exn -> TerrabuildException.Raise($"Process '{command} {args} in directory '{workingDir}' failed, exn")

type BuildQueue(maxItems: int) =
    let completion = new System.Threading.ManualResetEvent(false)
    let queueLock = obj()
    let queue = Queue<( (unit -> unit) )>()
    let mutable totalTasks = 0
    let mutable inFlight = 0

    member _.Enqueue (action: unit -> unit) =
        let rec trySchedule () =
            match queue.Count, inFlight with
            | (0, 0) ->
                completion.Set() |> ignore
            | (n, _) when 0 < n && inFlight < maxItems ->
                // feed the pipe the most we can
                let rec schedule() =
                    inFlight <- inFlight + 1
                    let action = queue.Dequeue()
                    async {
                        action()
                        lock queueLock (fun () ->
                            inFlight <- inFlight - 1
                            trySchedule()
                        )
                    } |> Async.Start
                    if 0 < queue.Count && inFlight < maxItems then
                        schedule()
                schedule()
            | _ -> ()

        lock queueLock (fun () ->
            totalTasks <- totalTasks + 1
            queue.Enqueue(action)
            trySchedule()
        )

    member _.WaitCompletion() =
        let enqueuedTasks = lock queueLock (fun () -> totalTasks)
        if enqueuedTasks > 0 then completion.WaitOne() |> ignore
