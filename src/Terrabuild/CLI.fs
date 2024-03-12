module CLI
open Argu

[<RequireQualifiedAccess>]
type ScafoldArgs =
    | [<Unique; AltCommandLine("--ws")>] Workspace of path:string
    | [<Unique>] Force
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Force -> "Default behavior is to not overwrite existing WORKSPACE or PROJECT file. This can be forced."

[<RequireQualifiedAccess>]
type RunArgs =
    | [<Unique; AltCommandLine("--ws")>] Workspace of path:string
    | [<Unique; AltCommandLine("--env")>] Environment of name:string
    | [<Unique; AltCommandLine("--par")>] Parallel of max:int
    | [<Unique; AltCommandLine("--nc")>] NoCache
    | [<Unique; AltCommandLine("--r")>] Retry
    | [<Unique; AltCommandLine("--l")>] Label of labels:string list
    | [<EqualsAssignment; AltCommandLine("--v")>] Variable of variable:string * value:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Environment _ -> "Environment to use."
            | Parallel _ -> "Max parallel build concurrency (default to number of processors)."
            | NoCache -> "Ignore cache when building target."
            | Retry -> "Retry failed task."
            | Label _-> "Select projects based on labels."
            | Variable _ -> "Set variable."

[<RequireQualifiedAccess>]
type TargetArgs =
    | [<Mandatory; ExactlyOnce; MainCommand; First>] Target of target:string list
    | [<Unique; AltCommandLine("--ws")>] Workspace of path:string
    | [<Unique; AltCommandLine("--env")>] Environment of name:string
    | [<Unique; AltCommandLine("--par")>] Parallel of max:int
    | [<Unique; AltCommandLine("--nc")>] NoCache
    | [<Unique; AltCommandLine("--r")>] Retry
    | [<Unique; AltCommandLine("--l")>] Label of labels:string list
    | [<EqualsAssignment; AltCommandLine("--v")>] Variable of variable:string * value:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "Specify build target."
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Environment _ -> "Environment to use."
            | Parallel _ -> "Max parallel build concurrency (default to number of processors)."
            | NoCache -> "Ignore cache when building target."
            | Retry -> "Retry failed task."
            | Label _-> "Select projects based on labels."
            | Variable _ -> "Set variable."

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
    | [<CliPrefix(CliPrefix.None)>] Scafold of ParseResults<ScafoldArgs>
    | [<CliPrefix(CliPrefix.None)>] Build of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Test of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Dist of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Publish of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Deploy of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Serve of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Run of ParseResults<TargetArgs>
    | [<CliPrefix(CliPrefix.None)>] Clear of ParseResults<ClearArgs>
    | [<Hidden; Unique; Inherit>] Debug
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Scafold _ -> "Scafold workspace."
            | Build _ -> "Run target 'build'."
            | Test _ -> "Run target 'test'."
            | Dist _ -> "Run target 'dist'."
            | Publish _ -> "Run target 'publish'."
            | Deploy _ -> "Run target 'deploy'."
            | Serve _ -> "Run target 'serve'."
            | Run _ -> "Run specified targets."
            | Clear _ -> "Clear specified caches."
            | Debug -> "Enable logging and debug dumps."
