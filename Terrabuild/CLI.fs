module CLI
open Argu

[<RequireQualifiedAccess>]
type BuildArgs =
    | [<Unique; Inherit>] Workspace of path:string
    | [<Unique; Inherit>] Parallel of max:int
    | [<Unique; Inherit>] Shared
    | [<Unique>] NoCache
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Parallel _ -> "Max parallel build concurrency (default 4)."
            | Shared -> "Local or shared execution."
            | NoCache -> "Do not use cache when building target."

[<RequireQualifiedAccess>]
type RunArgs =
    | [<MainCommand; ExactlyOnce; First>] Target of target:string
    | [<Unique; Inherit>] Workspace of path:string
    | [<Unique; Inherit>] Parallel of max:int
    | [<Unique; Inherit>] Shared
    | [<Unique>] NoCache
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "Specify build target."
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
            | Parallel _ -> "Max parallel build concurrency (default 4)."
            | Shared -> "Local or shared execution."
            | NoCache -> "Do not use cache when building target."

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
    | [<CliPrefix(CliPrefix.None)>] Build of ParseResults<BuildArgs>
    | [<CliPrefix(CliPrefix.None)>] Dist of ParseResults<BuildArgs>
    | [<CliPrefix(CliPrefix.None)>] Serve of ParseResults<BuildArgs>
    | [<CliPrefix(CliPrefix.None)>] Run of ParseResults<RunArgs>
    | [<CliPrefix(CliPrefix.None)>] Clear of ParseResults<ClearArgs>
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Build _ -> "Run target 'build'."
            | Dist _ -> "Run target 'dist'."
            | Serve _ -> "Run target 'serve'."
            | Run _ -> "Run specified targets."
            | Clear _ -> "Clear specified caches."
