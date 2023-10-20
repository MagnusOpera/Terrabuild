module CLI
open Argu

[<RequireQualifiedAccess>]
type RunArgs =
    | [<Unique; AltCommandLine("--ws")>] Workspace of path:string
    | [<Unique; AltCommandLine("--par")>] Parallel of max:int
    | [<Unique; AltCommandLine("--s")>] Shared
    | [<Unique; AltCommandLine("--env")>] Environment of name:string
    | [<Unique; AltCommandLine("--nc")>] NoCache
    | [<Unique; AltCommandLine("--r")>] Retry
    | [<Unique; AltCommandLine("--l")>] Label of labels:string list
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Parallel _ -> "Max parallel build concurrency (default to number of processors)."
            | Shared -> "Local or shared execution."
            | Environment _ -> "Environment to use."
            | NoCache -> "Do not use cache when building target."
            | Retry -> "Retry failed task."
            | Label _-> "Select projects based on labels."

[<RequireQualifiedAccess>]
type TargetArgs =
    | [<Mandatory; ExactlyOnce; MainCommand; First>] Target of target:string list
    | [<Unique; AltCommandLine("--ws")>] Workspace of path:string
    | [<Unique; AltCommandLine("--par")>] Parallel of max:int
    | [<Unique; AltCommandLine("--s")>] Shared
    | [<Unique; AltCommandLine("--env")>] Environment of name:string
    | [<Unique; AltCommandLine("--nc")>] NoCache
    | [<Unique; AltCommandLine("--r")>] Retry
    | [<Unique; AltCommandLine("--l")>] Label of labels:string list
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "Specify build target."
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Parallel _ -> "Max parallel build concurrency (default to number of processors)."
            | Shared -> "Local or shared execution."
            | Environment _ -> "Environment to use."
            | NoCache -> "Do not use cache when building target."
            | Retry -> "Retry failed task."
            | Label _-> "Select projects based on labels."

[<RequireQualifiedAccess>]
type ClearArgs =
    | [<ExactlyOnce>] BuildCache
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | BuildCache -> "Clear build cache."

[<RequireQualifiedAccess>]
type TerrabuildArgs =
    | [<CliPrefix(CliPrefix.None)>] Build of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Test of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Dist of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Serve of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Run of ParseResults<TargetArgs>
    | [<CliPrefix(CliPrefix.None)>] Clear of ParseResults<ClearArgs>
    | [<Hidden; Unique; Inherit>] Debug
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Build _ -> "Run target 'build'."
            | Test _ -> "Run target 'test'."
            | Dist _ -> "Run target 'dist'."
            | Serve _ -> "Run target 'serve'."
            | Run _ -> "Run specified targets."
            | Clear _ -> "Clear specified caches."
            | Debug -> "Enable logging and debug dumps."
