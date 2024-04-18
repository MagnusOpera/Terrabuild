module CLI
open Argu

[<RequireQualifiedAccess>]
type ScaffoldArgs =
    | [<Unique; AltCommandLine("-w")>] Workspace of path:string
    | [<Unique>] Force
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Force -> "Default behavior is to not overwrite existing WORKSPACE or PROJECT file. This can be forced."

[<RequireQualifiedAccess>]
type RunArgs =
    | [<Unique; AltCommandLine("-w")>] Workspace of path:string
    | [<Unique; AltCommandLine("-e")>] Environment of name:string
    | [<EqualsAssignment; AltCommandLine("-v")>] Variable of variable:string * value:string
    | [<Unique; AltCommandLine("-l")>] Label of labels:string list
    | [<Unique; AltCommandLine("-p")>] Parallel of max:int
    | [<Unique>] Local
    | [<Unique>] Force
    | [<Unique>] Retry
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Environment _ -> "Environment to use."
            | Parallel _ -> "Max parallel build concurrency (default to number of processors)."
            | Variable _ -> "Set variable."
            | Label _-> "Select projects based on labels."
            | Local -> "Local build - use no CI information."
            | Force -> "Ignore cache when building target."
            | Retry -> "Retry failed task."

[<RequireQualifiedAccess>]
type TargetArgs =
    | [<Mandatory; ExactlyOnce; MainCommand; First>] Target of target:string list
    | [<Unique; AltCommandLine("-w")>] Workspace of path:string
    | [<Unique; AltCommandLine("-e")>] Environment of name:string
    | [<EqualsAssignment; AltCommandLine("-v")>] Variable of variable:string * value:string
    | [<Unique; AltCommandLine("-l")>] Label of labels:string list
    | [<Unique; AltCommandLine("-p")>] Parallel of max:int
    | [<Unique>] Local
    | [<Unique>] Force
    | [<Unique>] Retry
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "Specify build target."
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Environment _ -> "Environment to use."
            | Parallel _ -> "Max parallel build concurrency (default to number of processors)."
            | Variable _ -> "Set variable."
            | Label _-> "Select projects based on labels."
            | Local -> "Local build - use no CI information."
            | Force -> "Ignore cache when building target."
            | Retry -> "Retry failed task."

[<RequireQualifiedAccess>]
type ClearArgs =
    | [<ExactlyOnce>] BuildCache
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | BuildCache -> "Clear build cache."

[<RequireQualifiedAccess>]
type LoginArgs =
    | [<Mandatory; ExactlyOnce>] Space of space:string
    | [<Mandatory; ExactlyOnce>] Token of token:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Space _ -> "Space slug"
            | Token _ -> "Token to connect to store"

[<RequireQualifiedAccess>]
type LogoutArgs =
    | [<Mandatory; ExactlyOnce>] Space of space:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Space _ -> "Space slug"

[<RequireQualifiedAccess>]
type TerrabuildArgs =
    | [<CliPrefix(CliPrefix.None)>] Scaffold of ParseResults<ScaffoldArgs>
    | [<CliPrefix(CliPrefix.None)>] Build of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Test of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Dist of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Publish of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Deploy of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Serve of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Run of ParseResults<TargetArgs>
    | [<CliPrefix(CliPrefix.None)>] Clear of ParseResults<ClearArgs>
    | [<CliPrefix(CliPrefix.None)>] Login of ParseResults<LoginArgs>
    | [<CliPrefix(CliPrefix.None)>] Logout of ParseResults<LogoutArgs>
    | [<Unique; Inherit>] WhatIf
    | [<Hidden; Unique; Inherit>] Debug
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Scaffold _ -> "Scaffold workspace."
            | Build _ -> "Run target 'build'."
            | Test _ -> "Run target 'test'."
            | Dist _ -> "Run target 'dist'."
            | Publish _ -> "Run target 'publish'."
            | Deploy _ -> "Run target 'deploy'."
            | Serve _ -> "Run target 'serve'."
            | Run _ -> "Run specified targets."
            | Clear _ -> "Clear specified caches."
            | Login _ -> "Log in to artifact store"
            | Logout _ -> "Disconnect from artifact store"
            | WhatIf -> "Prepare the action but do not apply."
            | Debug -> "Enable logging and debug dumps."
