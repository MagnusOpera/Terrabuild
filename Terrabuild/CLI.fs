module CLI
open Argu

[<RequireQualifiedAccess>]
type BuildArgs =
    | [<AltCommandLine("--nc"); Unique>] NoCache
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | NoCache -> "Do not use cache when building target."

[<RequireQualifiedAccess>]
type RunArgs =
    | [<MainCommand; ExactlyOnce; First>] Target of target:string
    | [<AltCommandLine("--nc"); Unique>] NoCache
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Target _ -> "Specify build target."
            | NoCache -> "Do not use cache when building target."

[<RequireQualifiedAccess>]
type TerrabuildArgs =
    | [<CliPrefix(CliPrefix.None)>] Build of ParseResults<BuildArgs>
    | [<CliPrefix(CliPrefix.None)>] Dist of ParseResults<BuildArgs>
    | [<CliPrefix(CliPrefix.None)>] Serve of ParseResults<BuildArgs>
    | [<CliPrefix(CliPrefix.None)>] Run of ParseResults<RunArgs>
    | [<AltCommandLine("--ws"); Unique; Inherit>] Workspace of path:string
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Build _ -> "Run target 'build'."
            | Dist _ -> "Run target 'dist'."
            | Serve _ -> "Run target 'serve'."
            | Run _ -> "Run specified target."
            | Workspace _ -> "Root of workspace. If not specified, current directory is used."
