module Exec
open System.Diagnostics
open System
open System.IO

type CaptureResult =
    | Success of string*int
    | Error of string*int

let execCaptureOutput (workingDir: string) (command: string) (args: string) =
    let psi = ProcessStartInfo (FileName = command,
                                Arguments = args,
                                UseShellExecute = false,
                                WorkingDirectory = workingDir,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true)

    use proc = Process.Start (psi)
    proc.WaitForExit()

    match proc.ExitCode with
    | 0 -> Success (proc.StandardOutput.ReadToEnd(), proc.ExitCode)
    | _ -> Error (proc.StandardError.ReadToEnd(), proc.ExitCode)

let execCaptureTimestampedOutput (workingDir: string) (command: string) (args: string) (logFile: string) =
    use logWriter = new StreamWriter(logFile)
    let writeLock = obj()
    let inline lockWrite (msg: string) = lock writeLock (fun () -> logWriter.WriteLine(msg))

    use proc = new Process()
    let psi = proc.StartInfo
    psi.FileName <- command
    psi.Arguments <- args
    psi.UseShellExecute <- false
    psi.WorkingDirectory <- workingDir
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    proc.OutputDataReceived.Add(fun e -> $"{DateTime.UtcNow}\tOUT\t{e.Data}" |> lockWrite)
    proc.ErrorDataReceived.Add(fun e -> $"{DateTime.UtcNow}\tERR\t{e.Data}" |> lockWrite)

    proc.Start() |> ignore
    proc.BeginOutputReadLine()
    proc.BeginErrorReadLine()
    proc.WaitForExit()

    proc.ExitCode
