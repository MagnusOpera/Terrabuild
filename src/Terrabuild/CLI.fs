module CLI
open Argu

[<RequireQualifiedAccess>]
type ContainerTool =
    | Docker
    | Podman
    | None

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
type LogsArgs =
    | [<ExactlyOnce; MainCommand; First>] Target of target:string list
    | [<Unique; AltCommandLine("-w")>] Workspace of path:string
    | [<Unique; AltCommandLine("-c")>] Configuration of name:string
    | [<EqualsAssignment; AltCommandLine("-v")>] Variable of variable:string * value:string
    | [<Unique; AltCommandLine("-l")>] Label of labels:string list
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "Specify build target."
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Configuration _ -> "Configuration to use."
            | Variable _ -> "Set variable."
            | Label _-> "Select projects based on labels."

[<RequireQualifiedAccess>]
type RunArgs =
    | [<ExactlyOnce; MainCommand; First>] Target of target:string list
    | [<Unique; AltCommandLine("-w")>] Workspace of path:string
    | [<Unique; AltCommandLine("-c")>] Configuration of name:string
    | [<EqualsAssignment; AltCommandLine("-v")>] Variable of variable:string * value:string
    | [<Unique; AltCommandLine("-l")>] Label of labels:string list
    | [<Unique; AltCommandLine("-p")>] Parallel of max:int
    | [<Unique; AltCommandLine("-f")>] Force
    | [<Unique; AltCommandLine("-r")>] Retry
    | [<Unique; AltCommandLine("-lo")>] Local_Only
    | [<Unique; AltCommandLine("-n")>] Note of note:string
    | [<Unique; AltCommandLine("-t")>] Tag of tag:string
    | [<Unique; AltCommandLine("-ct")>] Container_Tool of tool:ContainerTool
    | [<Unique>] Logs
    | [<Unique; Inherit>] WhatIf
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "Specify build target."
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Configuration _ -> "Configuration to use."
            | Parallel _ -> "Max parallel build concurrency (default to number of processors)."
            | Variable _ -> "Set variable."
            | Label _-> "Select projects based on labels."
            | Force -> "Ignore cache when building target."
            | Retry -> "Retry failed task."
            | Local_Only -> "Use local cache only."
            | Note _ -> "Note for the build."
            | Logs -> "Output logs for impacted projects."
            | Tag _ -> "Tag for build."
            | Container_Tool _ -> "Container Tool to use (docker or podman)."
            | WhatIf -> "Prepare the action but do not apply."

[<RequireQualifiedAccess>]
type ServeArgs =
    | [<Unique; AltCommandLine("-w")>] Workspace of path:string
    | [<Unique; AltCommandLine("-c")>] Configuration of name:string
    | [<EqualsAssignment; AltCommandLine("-v")>] Variable of variable:string * value:string
    | [<Unique; AltCommandLine("-l")>] Label of labels:string list
    | [<Unique>] Logs
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Configuration _ -> "Configuration to use."
            | Variable _ -> "Set variable."
            | Label _-> "Select projects based on labels."
            | Logs -> "Output logs for impacted projects."


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
    | [<ExactlyOnce>] Workspace of id:string
    | [<ExactlyOnce>] Token of token:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Workspace _ -> "Slug of space to connect to"
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
    | [<CliPrefix(CliPrefix.None)>] Logs of ParseResults<LogsArgs>
    | [<CliPrefix(CliPrefix.None)>] Run of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Serve of ParseResults<ServeArgs>
    | [<CliPrefix(CliPrefix.None)>] Clear of ParseResults<ClearArgs>
    | [<CliPrefix(CliPrefix.None)>] Login of ParseResults<LoginArgs>
    | [<CliPrefix(CliPrefix.None)>] Logout of ParseResults<LogoutArgs>
    | [<CliPrefix(CliPrefix.None)>] Version
    | [<Hidden; Unique; Inherit>] Debug
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Scaffold _ -> "Scaffold workspace."
            | Logs _ -> "dump logs."
            | Run _ -> "Run specified targets."
            | Serve _ -> "Serve specified targets."
            | Clear _ -> "Clear specified caches."
            | Login _ -> "Connect to backend."
            | Logout _ -> "Disconnect from backend."
            | Version -> "Show current Terrabuild version."
            | Debug -> "Enable logging and debug dumps."
