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
    | [<Unique; AltCommandLine("-c")>] Configuration of name:string
    | [<Unique; AltCommandLine("-e")>] Environment of name:string
    | [<EqualsAssignment; AltCommandLine("-v")>] Variable of variable:string * value:string
    | [<Unique; AltCommandLine("-l")>] Label of labels:string list
    | [<Unique; AltCommandLine("-p")>] Parallel of max:int
    | [<Unique; AltCommandLine("-f")>] Force
    | [<Unique; AltCommandLine("-r")>] Retry
    | [<Unique; AltCommandLine("-lo")>] LocalOnly
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Configuration _ -> "Configuration to use."
            | Environment _ -> "Environment to use."
            | Parallel _ -> "Max parallel build concurrency (default to number of processors)."
            | Variable _ -> "Set variable."
            | Label _-> "Select projects based on labels."
            | Force -> "Ignore cache when building target."
            | Retry -> "Retry failed task."
            | LocalOnly -> "Use local cache only."

[<RequireQualifiedAccess>]
type TargetArgs =
    | [<ExactlyOnce; MainCommand; First>] Target of target:string list
    | [<Unique; AltCommandLine("-w")>] Workspace of path:string
    | [<Unique; AltCommandLine("-c")>] Configuration of name:string
    | [<Unique; AltCommandLine("-e")>] Environment of name:string
    | [<EqualsAssignment; AltCommandLine("-v")>] Variable of variable:string * value:string
    | [<Unique; AltCommandLine("-l")>] Label of labels:string list
    | [<Unique; AltCommandLine("-p")>] Parallel of max:int
    | [<Unique; AltCommandLine("-f")>] Force
    | [<Unique; AltCommandLine("-r")>] Retry
    | [<Unique; AltCommandLine("-lo")>] LocalOnly
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "Specify build target."
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Configuration _ -> "Configuration to use."
            | Environment _ -> "Environment to use."
            | Parallel _ -> "Max parallel build concurrency (default to number of processors)."
            | Variable _ -> "Set variable."
            | Label _-> "Select projects based on labels."
            | Force -> "Ignore cache when building target."
            | Retry -> "Retry failed task."
            | LocalOnly -> "Use local cache only."

[<RequireQualifiedAccess>]
type ClearArgs =
    | [<Unique>] Cache
    | [<Unique>] Home
    | [<Unique>] All
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Cache -> "Clear build cache."
            | Home -> "Clear home cache."
            | All -> "Clear all caches."

[<RequireQualifiedAccess>]
type LoginArgs =
    | [<ExactlyOnce>] Space of space:string
    | [<ExactlyOnce>] Token of token:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Space _ -> "Slug of space to connect to"
            | Token _ -> "Token to connect to space"

[<RequireQualifiedAccess>]
type LogoutArgs =
    | [<ExactlyOnce>] Space of space:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Space _ -> "Slug of space to remove"

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
    | [<CliPrefix(CliPrefix.None)>] Version
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
            | Login _ -> "Connect to backend."
            | Logout _ -> "Disconnect from backend."
            | Version -> "Show current Terrabuild version."
            | WhatIf -> "Prepare the action but do not apply."
            | Debug -> "Enable logging and debug dumps."
